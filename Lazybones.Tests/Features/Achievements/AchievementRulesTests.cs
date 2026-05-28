using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Features.Achievements;
using Lazybones.Features.History;
using Lazybones.Tests.Features.History;
using Xunit;

namespace Lazybones.Tests.Features.Achievements;

public class AchievementRulesTests
{
    private static readonly DateOnly Today = new(2026, 5, 26);

    private static CycleRecord StandingNaturalEngaged(
        DateTime? startedAt = null,
        int durationMinutes = 30,
        int responseDelaySeconds = 1,
        bool promptDismissed = false)
    {
        var started = startedAt ?? Today.ToDateTime(new TimeOnly(10, 0));
        return new CycleRecord
        {
            StartedAt = started,
            EndedAt = started.AddMinutes(durationMinutes),
            WasStanding = true,
            Outcome = CycleOutcome.CompletedNaturally,
            ActualDurationSeconds = durationMinutes * 60,
            PlannedDurationSeconds = durationMinutes * 60,
            ResponseDelaySeconds = responseDelaySeconds,
            PromptDismissed = promptDismissed
        };
    }

    private static IReadOnlyList<Achievement> Evaluate(
        CycleRecord record,
        FakeHistoryStore history,
        int dailyCycleGoal = 3,
        params string[] alreadyUnlocked)
    {
        history.Append(record);
        return AchievementRules.EvaluateNewlyUnlocked(record, history, dailyCycleGoal, alreadyUnlocked, Today, TimeSpan.Zero);
    }

    private static bool Unlocked(IReadOnlyList<Achievement> result, string id) =>
        result.Any(a => a.Id == id);

    [Fact]
    public void FirstStand_unlocks_on_first_natural_engaged_standing_cycle()
    {
        var result = Evaluate(StandingNaturalEngaged(), new FakeHistoryStore());
        Assert.True(Unlocked(result, AchievementCatalog.FirstStandId));
    }

    [Fact]
    public void FirstStand_does_not_unlock_for_sitting_cycle()
    {
        var record = StandingNaturalEngaged();
        record.WasStanding = false;
        var result = Evaluate(record, new FakeHistoryStore());
        Assert.False(Unlocked(result, AchievementCatalog.FirstStandId));
    }

    [Fact]
    public void FirstStand_does_not_unlock_for_toggled_cycle()
    {
        var record = StandingNaturalEngaged();
        record.Outcome = CycleOutcome.Toggled;
        var result = Evaluate(record, new FakeHistoryStore());
        Assert.False(Unlocked(result, AchievementCatalog.FirstStandId));
    }

    [Fact]
    public void FirstStand_does_not_unlock_when_already_owned()
    {
        var result = Evaluate(StandingNaturalEngaged(), new FakeHistoryStore(),
            alreadyUnlocked: AchievementCatalog.FirstStandId);
        Assert.False(Unlocked(result, AchievementCatalog.FirstStandId));
    }

    [Fact]
    public void Engagement_window_excludes_long_response_delays()
    {
        // 6 minutes > 5-minute engagement window — FirstStand should NOT unlock.
        var record = StandingNaturalEngaged(responseDelaySeconds: 6 * 60);
        var result = Evaluate(record, new FakeHistoryStore());
        Assert.False(Unlocked(result, AchievementCatalog.FirstStandId));
    }

    [Fact]
    public void QuickDraw_unlocks_within_10_seconds()
    {
        var record = StandingNaturalEngaged(responseDelaySeconds: 8);
        var result = Evaluate(record, new FakeHistoryStore());
        Assert.True(Unlocked(result, AchievementCatalog.QuickDrawId));
    }

    [Fact]
    public void QuickDraw_does_not_unlock_at_11_seconds()
    {
        var record = StandingNaturalEngaged(responseDelaySeconds: 11);
        var result = Evaluate(record, new FakeHistoryStore());
        Assert.False(Unlocked(result, AchievementCatalog.QuickDrawId));
    }

    [Fact]
    public void IronLegs_requires_30min_planned_and_no_dismissal()
    {
        var ok = StandingNaturalEngaged(durationMinutes: 30);
        Assert.True(Unlocked(Evaluate(ok, new FakeHistoryStore()), AchievementCatalog.IronLegsId));

        var tooShort = StandingNaturalEngaged(durationMinutes: 20);
        Assert.False(Unlocked(Evaluate(tooShort, new FakeHistoryStore()), AchievementCatalog.IronLegsId));

        var dismissed = StandingNaturalEngaged(durationMinutes: 30, promptDismissed: true);
        Assert.False(Unlocked(Evaluate(dismissed, new FakeHistoryStore()), AchievementCatalog.IronLegsId));
    }

    [Fact]
    public void EarlyBird_unlocks_for_cycle_started_before_9am()
    {
        var early = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8, 30)));
        Assert.True(Unlocked(Evaluate(early, new FakeHistoryStore()), AchievementCatalog.EarlyBirdId));

        var notEarly = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(9, 30)));
        Assert.False(Unlocked(Evaluate(notEarly, new FakeHistoryStore()), AchievementCatalog.EarlyBirdId));
    }

    [Fact]
    public void NightOwl_unlocks_for_cycle_ended_at_or_after_10pm()
    {
        var late = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(21, 35)));
        Assert.True(Unlocked(Evaluate(late, new FakeHistoryStore()), AchievementCatalog.NightOwlId));

        var notLate = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(20, 0)));
        Assert.False(Unlocked(Evaluate(notLate, new FakeHistoryStore()), AchievementCatalog.NightOwlId));
    }

    [Fact]
    public void DailyDriver_unlocks_at_five_completed_or_toggled_standing_cycles_today()
    {
        var history = new FakeHistoryStore();
        // 4 standing cycles already today; the 5th is justAppended.
        for (var i = 0; i < 4; i++)
            history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8 + i, 0))));

        var fifth = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(13, 0)));
        var result = Evaluate(fifth, history);
        Assert.True(Unlocked(result, AchievementCatalog.DailyDriverId));
    }

    [Fact]
    public void Overachiever_unlocks_at_150_percent_of_goal()
    {
        var history = new FakeHistoryStore();
        // Goal=4 → 1.5x = 6 cycles. Append 5 prior + 1 just now = 6.
        for (var i = 0; i < 5; i++)
            history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8 + i, 0))));

        var sixth = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(14, 0)));
        var result = Evaluate(sixth, history, dailyCycleGoal: 4);
        Assert.True(Unlocked(result, AchievementCatalog.OverachieverId));
    }

    [Fact]
    public void DoubleDown_unlocks_at_200_percent_of_goal()
    {
        var history = new FakeHistoryStore();
        // Goal=3 → 2x = 6 cycles.
        for (var i = 0; i < 5; i++)
            history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8 + i, 0))));

        var sixth = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(14, 0)));
        var result = Evaluate(sixth, history, dailyCycleGoal: 3);
        Assert.True(Unlocked(result, AchievementCatalog.DoubleDownId));
    }

    [Fact]
    public void PerfectDay_requires_goal_met_and_no_dismissals()
    {
        var history = new FakeHistoryStore();
        // 2 prior dismiss-free cycles + the just-appended one = 3 standing, none dismissed.
        for (var i = 0; i < 2; i++)
            history.Append(StandingNaturalEngaged(
                Today.ToDateTime(new TimeOnly(8 + i, 0)), promptDismissed: false));

        var third = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(13, 0)));
        var result = Evaluate(third, history, dailyCycleGoal: 3);
        Assert.True(Unlocked(result, AchievementCatalog.PerfectDayId));
    }

    [Fact]
    public void PerfectDay_blocked_by_any_dismissed_prompt_today()
    {
        var history = new FakeHistoryStore();
        history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8, 0)), promptDismissed: true));
        history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(10, 0))));

        var third = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(13, 0)));
        var result = Evaluate(third, history, dailyCycleGoal: 3);
        Assert.False(Unlocked(result, AchievementCatalog.PerfectDayId));
    }

    [Fact]
    public void Centurion_unlocks_at_100_lifetime_standing_cycles()
    {
        var history = new FakeHistoryStore();
        // 99 prior + 1 just-appended = 100.
        for (var i = 0; i < 99; i++)
            history.Append(StandingNaturalEngaged(Today.AddDays(-i / 3).ToDateTime(new TimeOnly(10, 0))));

        var hundredth = StandingNaturalEngaged();
        var result = Evaluate(hundredth, history);
        Assert.True(Unlocked(result, AchievementCatalog.CenturionId));
    }

    [Fact]
    public void LongHaul_unlocks_at_10_lifetime_standing_hours()
    {
        var history = new FakeHistoryStore();
        // 19 prior 30-min cycles = 570 min. Adding one more 30-min cycle = 600 min = 10h.
        for (var i = 0; i < 19; i++)
            history.Append(StandingNaturalEngaged(
                Today.AddDays(-i / 3).ToDateTime(new TimeOnly(10, 0)), durationMinutes: 30));

        var twentieth = StandingNaturalEngaged(durationMinutes: 30);
        var result = Evaluate(twentieth, history);
        Assert.True(Unlocked(result, AchievementCatalog.LongHaulId));
    }

    [Fact]
    public void WarmingUp_unlocks_at_three_day_streak()
    {
        var history = new FakeHistoryStore();
        // 3 standing cycles for each of yesterday and the day before.
        for (var dayOffset = 1; dayOffset <= 2; dayOffset++)
            for (var i = 0; i < 3; i++)
                history.Append(StandingNaturalEngaged(
                    Today.AddDays(-dayOffset).ToDateTime(new TimeOnly(8 + i, 0))));

        // Today: 2 prior + 1 just-now = 3 → today qualifies → streak = 3.
        for (var i = 0; i < 2; i++)
            history.Append(StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(8 + i, 0))));

        var third = StandingNaturalEngaged(Today.ToDateTime(new TimeOnly(13, 0)));
        var result = Evaluate(third, history, dailyCycleGoal: 3);
        Assert.True(Unlocked(result, AchievementCatalog.WarmingUpId));
    }

    [Fact]
    public void Unrelated_achievements_do_not_fire_for_a_short_standing_cycle()
    {
        var record = StandingNaturalEngaged(durationMinutes: 1);
        var result = Evaluate(record, new FakeHistoryStore());

        // FirstStand should fire, but Iron Legs should not.
        Assert.True(Unlocked(result, AchievementCatalog.FirstStandId));
        Assert.False(Unlocked(result, AchievementCatalog.IronLegsId));
    }
}
