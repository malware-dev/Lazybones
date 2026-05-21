using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Features.History;

namespace Lazybones.Features.Achievements;

public static class AchievementRules
{
    // Cycles that the user responded to within this many seconds count as
    // "engaged". Beyond this, the dialog was likely left sitting (e.g. AFK
    // or overnight) and engagement-based achievements shouldn't fire.
    private const int EngagementResponseWindowSeconds = 5 * 60;

    // Quick Draw is stricter than the generic engagement window.
    private const int QuickDrawResponseSeconds = 10;

    public static IReadOnlyList<Achievement> EvaluateNewlyUnlocked(
        CycleRecord justAppended,
        IHistoryStore history,
        int dailyCycleGoal,
        ICollection<string> alreadyUnlocked,
        DateOnly today)
    {
        var ctx = new EvalContext(justAppended, history, dailyCycleGoal, today);

        var newly = new List<Achievement>();
        foreach (var ach in AchievementCatalog.All)
        {
            if (alreadyUnlocked.Contains(ach.Id)) continue;
            if (Qualifies(ach.Id, ctx))
                newly.Add(ach);
        }
        return newly;
    }

    private static bool Qualifies(string id, EvalContext c)
    {
        var record = c.Record;
        var natural = record.Outcome == CycleOutcome.CompletedNaturally;
        var standingNatural = record.WasStanding && natural;
        var engaged = record.ResponseDelaySeconds <= EngagementResponseWindowSeconds;
        var standingNaturalEngaged = standingNatural && engaged;

        return id switch
        {
            AchievementCatalog.FirstStandId =>
                standingNaturalEngaged,

            AchievementCatalog.QuickDrawId =>
                natural && record.ResponseDelaySeconds <= QuickDrawResponseSeconds,

            AchievementCatalog.IronLegsId =>
                standingNaturalEngaged && record.PlannedDurationSeconds >= 30 * 60 && !record.PromptDismissed,

            AchievementCatalog.EarlyBirdId =>
                standingNaturalEngaged && record.StartedAt.Hour < 9,

            AchievementCatalog.NightOwlId =>
                standingNaturalEngaged && record.EndedAt.Hour >= 22,

            AchievementCatalog.WarmingUpId =>
                c.CurrentStreak >= 3,

            AchievementCatalog.SevenDayStreakId =>
                c.CurrentStreak >= 7,

            AchievementCatalog.TwoWeekWonderId =>
                c.CurrentStreak >= 14,

            AchievementCatalog.HabitFormedId =>
                c.CurrentStreak >= 30,

            AchievementCatalog.DailyDriverId =>
                c.TodayCycleCount >= 5,

            AchievementCatalog.OverachieverId =>
                c.TodayStandingCycles >= c.DailyCycleGoal * 1.5,

            AchievementCatalog.DoubleDownId =>
                c.TodayStandingCycles >= c.DailyCycleGoal * 2,

            AchievementCatalog.PerfectDayId =>
                c.TodayStandingCycles >= c.DailyCycleGoal
                && c.History.GetDay(c.Today).All(r => !r.PromptDismissed),

            AchievementCatalog.CenturionId =>
                c.LifetimeStandingCycles >= 100,

            AchievementCatalog.LongHaulId =>
                c.LifetimeStandingMinutes >= 10 * 60,

            AchievementCatalog.MountaineerId =>
                c.LifetimeStandingMinutes >= 100 * 60,

            _ => false
        };
    }

    private sealed class EvalContext
    {
        public EvalContext(CycleRecord record, IHistoryStore history, int dailyCycleGoal, DateOnly today)
        {
            Record = record;
            History = history;
            DailyCycleGoal = dailyCycleGoal;
            Today = today;
        }

        public CycleRecord Record { get; }
        public IHistoryStore History { get; }
        public int DailyCycleGoal { get; }
        public DateOnly Today { get; }

        private int? _currentStreak;
        public int CurrentStreak => _currentStreak ??= StreakCalculator.CalculateCurrent(History, DailyCycleGoal, Today);

        private int? _todayStandingMinutes;
        public int TodayStandingMinutes => _todayStandingMinutes ??= History.GetTodayStandingMinutes();

        private int? _todayStandingCycles;
        public int TodayStandingCycles => _todayStandingCycles ??= History.GetTodayStandingCycles();

        private int? _todayCycleCount;
        public int TodayCycleCount => _todayCycleCount ??=
            History.GetDay(Today).Count(r => r.WasStanding && r.Outcome != CycleOutcome.Reset);

        private IReadOnlyList<CycleRecord>? _allRecords;
        private IReadOnlyList<CycleRecord> AllRecords => _allRecords ??= History.GetAll();

        private int? _lifetimeStandingCycles;
        public int LifetimeStandingCycles => _lifetimeStandingCycles ??=
            AllRecords.Count(r => r.WasStanding && r.Outcome != CycleOutcome.Reset);

        private int? _lifetimeStandingMinutes;
        public int LifetimeStandingMinutes => _lifetimeStandingMinutes ??=
            AllRecords.Where(r => r.WasStanding).Sum(r => r.ActualDurationSeconds) / 60;
    }
}
