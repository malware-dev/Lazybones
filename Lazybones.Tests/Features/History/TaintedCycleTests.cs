using System;
using Lazybones.Features.Achievements;
using Lazybones.Features.History;
using Xunit;

namespace Lazybones.Tests.Features.History;

/// <summary>
/// Cycles where the user edited the clock mid-cycle (WasTimeEdited = true)
/// must not contribute to achievements, the daily-goal count, or streaks.
/// These tests pin the gates across all the spots a tainted record could leak in.
/// </summary>
public class TaintedCycleTests
{
    private static readonly DateOnly Today = new(2026, 5, 26);

    private static CycleRecord StandingNaturalEngaged(
        DateOnly? day = null,
        bool tainted = false,
        int durationMinutes = 30,
        int responseDelaySeconds = 1)
    {
        var started = (day ?? Today).ToDateTime(new TimeOnly(10, 0));
        return new CycleRecord
        {
            StartedAt = started,
            EndedAt = started.AddMinutes(durationMinutes),
            WasStanding = true,
            Outcome = CycleOutcome.CompletedNaturally,
            PlannedDurationSeconds = durationMinutes * 60,
            ActualDurationSeconds = durationMinutes * 60,
            ResponseDelaySeconds = responseDelaySeconds,
            WasTimeEdited = tainted
        };
    }

    [Fact]
    public void Tainted_cycle_does_not_unlock_FirstStand()
    {
        var history = new FakeHistoryStore();
        var record = StandingNaturalEngaged(tainted: true);
        history.Append(record);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(record, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.FirstStandId);
    }

    [Fact]
    public void Tainted_cycle_does_not_unlock_IronLegs()
    {
        var history = new FakeHistoryStore();
        var record = StandingNaturalEngaged(tainted: true, durationMinutes: 30);
        history.Append(record);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(record, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.IronLegsId);
    }

    [Fact]
    public void Tainted_cycle_does_not_unlock_QuickDraw()
    {
        var history = new FakeHistoryStore();
        var record = StandingNaturalEngaged(tainted: true, responseDelaySeconds: 2);
        history.Append(record);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(record, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.QuickDrawId);
    }

    [Fact]
    public void Untainted_cycle_still_unlocks_FirstStand()
    {
        var history = new FakeHistoryStore();
        var record = StandingNaturalEngaged(tainted: false);
        history.Append(record);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(record, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.Contains(unlocked, a => a.Id == AchievementCatalog.FirstStandId);
    }

    [Fact]
    public void Tainted_cycles_do_not_count_toward_DailyDriver()
    {
        var history = new FakeHistoryStore();
        // 4 tainted standing cycles today + 1 untainted = 1 valid completed
        // standing cycle. DailyDriver needs 5 untainted completions; this fails.
        for (var i = 0; i < 4; i++)
            history.Append(StandingNaturalEngaged(tainted: true));
        var fifth = StandingNaturalEngaged(tainted: false);
        history.Append(fifth);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(fifth, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.DailyDriverId);
    }

    [Fact]
    public void Tainted_cycles_do_not_count_toward_Centurion()
    {
        var history = new FakeHistoryStore();
        // 99 tainted lifetime cycles + 1 untainted just-appended = 1 toward Centurion.
        for (var i = 0; i < 99; i++)
            history.Append(StandingNaturalEngaged(day: Today.AddDays(-i / 3), tainted: true));
        var hundredth = StandingNaturalEngaged(tainted: false);
        history.Append(hundredth);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(hundredth, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.CenturionId);
    }

    [Fact]
    public void Tainted_minutes_excluded_from_LongHaul()
    {
        var history = new FakeHistoryStore();
        // 19 tainted 30-min cycles + 1 untainted 30-min = 30 untainted minutes total.
        // LongHaul needs 600.
        for (var i = 0; i < 19; i++)
            history.Append(StandingNaturalEngaged(
                day: Today.AddDays(-i / 3), tainted: true, durationMinutes: 30));
        var twentieth = StandingNaturalEngaged(tainted: false, durationMinutes: 30);
        history.Append(twentieth);

        var unlocked = AchievementRules.EvaluateNewlyUnlocked(twentieth, history, dailyCycleGoal: 3,
            alreadyUnlocked: Array.Empty<string>(), today: Today);

        Assert.DoesNotContain(unlocked, a => a.Id == AchievementCatalog.LongHaulId);
    }

    [Fact]
    public void Tainted_cycles_do_not_count_toward_streak()
    {
        var history = new FakeHistoryStore();
        // Yesterday + day before: 3 untainted standing cycles each ✓
        for (var dayOffset = 1; dayOffset <= 2; dayOffset++)
            for (var i = 0; i < 3; i++)
                history.Append(StandingNaturalEngaged(day: Today.AddDays(-dayOffset), tainted: false));

        // Today: 3 cycles but all tainted → goal not met → streak should count
        // yesterday + day before (2 days) but exclude today.
        for (var i = 0; i < 3; i++)
            history.Append(StandingNaturalEngaged(tainted: true));

        var streak = StreakCalculator.CalculateCurrent(history, dailyCycleGoal: 3, Today);
        Assert.Equal(2, streak);
    }

    [Fact]
    public void Tainted_only_day_in_middle_breaks_streak()
    {
        var history = new FakeHistoryStore();
        // Today: 3 ✓
        // Yesterday: 3 ✓
        // 2 days ago: 3 tainted (active day below goal — breaks streak)
        // 3 days ago: 3 ✓ (unreachable past the break)
        for (var i = 0; i < 3; i++) history.Append(StandingNaturalEngaged(tainted: false));
        for (var i = 0; i < 3; i++) history.Append(StandingNaturalEngaged(day: Today.AddDays(-1), tainted: false));
        for (var i = 0; i < 3; i++) history.Append(StandingNaturalEngaged(day: Today.AddDays(-2), tainted: true));
        for (var i = 0; i < 3; i++) history.Append(StandingNaturalEngaged(day: Today.AddDays(-3), tainted: false));

        var streak = StreakCalculator.CalculateCurrent(history, dailyCycleGoal: 3, Today);
        Assert.Equal(2, streak);
    }

    [Fact]
    public void CompletedStandingCyclesOn_excludes_tainted()
    {
        var history = new FakeHistoryStore();
        history.Append(StandingNaturalEngaged(tainted: false));
        history.Append(StandingNaturalEngaged(tainted: false));
        history.Append(StandingNaturalEngaged(tainted: true));
        history.Append(StandingNaturalEngaged(tainted: true));

        Assert.Equal(2, history.CompletedStandingCyclesOn(Today));
    }

    [Fact]
    public void StandingMinutesOn_includes_tainted()
    {
        // Stat display reflects reality — the user did stand for those minutes,
        // they just don't count toward achievements. The minutes-today number
        // and the heatmap intensity should include tainted cycles.
        var history = new FakeHistoryStore();
        history.Append(StandingNaturalEngaged(durationMinutes: 30, tainted: false));
        history.Append(StandingNaturalEngaged(durationMinutes: 20, tainted: true));

        Assert.Equal(50, history.StandingMinutesOn(Today));
    }
}
