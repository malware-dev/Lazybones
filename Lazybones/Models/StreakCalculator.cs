using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Services;

namespace Lazybones.Models;

public static class StreakCalculator
{
    // Duolingo-style: 1 missed day per rolling 7-day window doesn't break the streak.
    // Today is treated as pending (neither hit nor miss) if its standing minutes are
    // still below the goal — we don't want a fresh morning to look like a missed day.
    public static int CalculateCurrent(IHistoryStore history, int dailyGoalMinutes, DateOnly today)
    {
        if (dailyGoalMinutes <= 0) return 0;

        var lookback = today.AddDays(-365);
        var minutesByDay = history.GetRange(lookback, today)
            .Where(r => r.WasStanding)
            .GroupBy(r => DateOnly.FromDateTime(r.EndedAt))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.ActualDurationSeconds) / 60);

        var startDay = today;
        var todayMinutes = minutesByDay.GetValueOrDefault(today, 0);
        if (todayMinutes < dailyGoalMinutes)
            startDay = today.AddDays(-1);

        var streak = 0;
        DateOnly? lastFreezeDay = null;

        for (var d = startDay; d >= lookback; d = d.AddDays(-1))
        {
            if (lastFreezeDay.HasValue && lastFreezeDay.Value.DayNumber - d.DayNumber > 6)
                lastFreezeDay = null;

            var minutes = minutesByDay.GetValueOrDefault(d, 0);
            if (minutes >= dailyGoalMinutes)
            {
                streak++;
            }
            else
            {
                if (lastFreezeDay == null)
                    lastFreezeDay = d;
                else
                    break;
            }
        }

        return streak;
    }
}
