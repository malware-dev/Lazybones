using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Core.State;

namespace Lazybones.Features.History;

public static class StreakCalculator
{
    // A "streak" = one full daily-goal completion (filling the outer ring
    // once). The streak count is the running TOTAL of completions from today
    // backwards, stopping at the first "kill" day — an active day where the
    // user did fewer than the goal's worth of cycles.
    //
    // Multiple completions in a single day count separately: 6 standing
    // cycles with a goal of 3 = 2 completions = +2 to the count.
    //
    // Day-counting rules:
    // - Today is pending: today's completions count, and today never kills
    //   (a day in progress isn't a "miss" yet).
    // - Paused days (no records at all) preserve the chain without adding.
    // - Active days with zero completions kill the chain — everything before
    //   them is unreachable.
    // - RolloverReset records don't count as "active" — the rollover is the
    //   app punching out a cycle, not the user.
    // - Tainted cycles (WasTimeEdited) don't count toward completions, but
    //   their presence still makes the day "active".
    public static int CalculateCurrent(IHistoryStore history, int dailyCycleGoal, DateOnly today, TimeSpan rolloverTime)
    {
        if (dailyCycleGoal <= 0) return 0;

        var lookback = today.AddDays(-365);
        var records = history.GetRange(lookback, today, rolloverTime);

        var cyclesByDay = records
            .Where(r => r.WasStanding && r.Outcome == CycleOutcome.CompletedNaturally && !r.WasTimeEdited)
            .GroupBy(r => LogicalDay.From(r.StartedAt, rolloverTime))
            .ToDictionary(g => g.Key, g => g.Count());

        var activeDays = new HashSet<DateOnly>(
            records
                .Where(r => r.Outcome != CycleOutcome.RolloverReset)
                .Select(r => LogicalDay.From(r.StartedAt, rolloverTime)));

        var completions = 0;
        for (var d = today; d >= lookback; d = d.AddDays(-1))
        {
            var cyclesOnDay = cyclesByDay.GetValueOrDefault(d, 0);
            var completionsOnDay = cyclesOnDay / dailyCycleGoal;
            completions += completionsOnDay;

            // Today never kills the chain — a partial day is pending, not
            // missed. Earlier days do: an active day with zero completions
            // resets the streak to whatever's been counted so far above it.
            if (d == today) continue;
            if (activeDays.Contains(d) && completionsOnDay == 0) break;
        }
        return completions;
    }
}
