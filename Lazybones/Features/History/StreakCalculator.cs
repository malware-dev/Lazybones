using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Core.State;

namespace Lazybones.Features.History;

public static class StreakCalculator
{
    // A streak day = an active logical day where the user completed at least
    // `dailyCycleGoal` standing cycles, dated by when each cycle STARTED.
    // Cycles ended early via Swap or Reset don't count (Outcome must be
    // CompletedNaturally). "Logical day" is determined by the configured
    // day-rollover time — for the default 06:00, a cycle started 02:30 belongs
    // to the previous logical day.
    //
    // No real-time loss action — the streak fails at end-of-day when an
    // active day finishes below the goal. Days with no records at all
    // (screen locked all day / app paused / machine off) are "paused" and
    // neither extend nor break the streak. Today is pending while still
    // below the goal — a fresh morning doesn't look like a miss.
    public static int CalculateCurrent(IHistoryStore history, int dailyCycleGoal, DateOnly today, TimeSpan rolloverTime)
    {
        if (dailyCycleGoal <= 0) return 0;

        var lookback = today.AddDays(-365);
        var records = history.GetRange(lookback, today, rolloverTime);

        // Count completed standing cycles per day, attributed by StartedAt's
        // logical day. Tainted cycles (WasTimeEdited) don't count toward the
        // daily goal — editing the clock invalidates the cycle.
        var cyclesByDay = records
            .Where(r => r.WasStanding && r.Outcome == CycleOutcome.CompletedNaturally && !r.WasTimeEdited)
            .GroupBy(r => LogicalDay.From(r.StartedAt, rolloverTime))
            .ToDictionary(g => g.Key, g => g.Count());

        // "Active days" = any cycle ever started on that logical date. Includes
        // sitting, abandoned, and time-edited cycles — we just need to know
        // whether Lazybones saw the user that day at all. A tainted-only day
        // is "active but below goal" and breaks the streak rather than being a
        // forgiven pause; otherwise editing the clock would be a free
        // streak-saver.
        //
        // RolloverReset records are EXCLUDED here: the rollover is the app
        // punching out the cycle, not the user, so a day on which the only
        // recorded activity is a rollover-reset is treated as paused, not
        // active-but-missed.
        var activeDays = new HashSet<DateOnly>(
            records
                .Where(r => r.Outcome != CycleOutcome.RolloverReset)
                .Select(r => LogicalDay.From(r.StartedAt, rolloverTime)));

        // Today is pending until the count hits the goal.
        var startDay = today;
        if (cyclesByDay.GetValueOrDefault(today, 0) < dailyCycleGoal)
            startDay = today.AddDays(-1);

        var streak = 0;
        for (var d = startDay; d >= lookback; d = d.AddDays(-1))
        {
            if (!activeDays.Contains(d)) continue; // paused day — skip
            if (cyclesByDay.GetValueOrDefault(d, 0) >= dailyCycleGoal)
                streak++;
            else
                break; // active day missed the goal
        }
        return streak;
    }
}
