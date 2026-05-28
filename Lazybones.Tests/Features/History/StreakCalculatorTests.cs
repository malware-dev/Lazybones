using System;
using Lazybones.Features.History;
using Xunit;

namespace Lazybones.Tests.Features.History;

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 5, 26);

    private static CycleRecord CompletedStanding(DateOnly day) => new()
    {
        StartedAt = day.ToDateTime(new TimeOnly(10, 0)),
        EndedAt = day.ToDateTime(new TimeOnly(10, 30)),
        WasStanding = true,
        Outcome = CycleOutcome.CompletedNaturally,
        ActualDurationSeconds = 30 * 60,
        PlannedDurationSeconds = 30 * 60
    };

    private static CycleRecord Sitting(DateOnly day) => new()
    {
        StartedAt = day.ToDateTime(new TimeOnly(11, 0)),
        EndedAt = day.ToDateTime(new TimeOnly(13, 0)),
        WasStanding = false,
        Outcome = CycleOutcome.CompletedNaturally,
        ActualDurationSeconds = 2 * 3600,
        PlannedDurationSeconds = 2 * 3600
    };

    private static CycleRecord ToggledStanding(DateOnly day) => new()
    {
        StartedAt = day.ToDateTime(new TimeOnly(14, 0)),
        EndedAt = day.ToDateTime(new TimeOnly(14, 10)),
        WasStanding = true,
        Outcome = CycleOutcome.Toggled,
        ActualDurationSeconds = 10 * 60,
        PlannedDurationSeconds = 30 * 60
    };

    [Fact]
    public void Zero_or_negative_goal_returns_zero()
    {
        var history = new FakeHistoryStore();
        Assert.Equal(0, StreakCalculator.CalculateCurrent(history, 0, Today, TimeSpan.Zero));
        Assert.Equal(0, StreakCalculator.CalculateCurrent(history, -5, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Empty_history_returns_zero()
    {
        Assert.Equal(0, StreakCalculator.CalculateCurrent(new FakeHistoryStore(), 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Today_at_goal_extends_streak()
    {
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today));
        Assert.Equal(1, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Today_below_goal_is_pending_not_a_miss()
    {
        // Yesterday hit goal; today has 1/3 cycles so far — streak should still
        // count yesterday (and earlier), not break.
        var history = new FakeHistoryStore();
        var yesterday = Today.AddDays(-1);
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(yesterday));
        history.Append(CompletedStanding(Today)); // 1 of 3 so far today

        Assert.Equal(1, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Consecutive_active_at_goal_days_chain()
    {
        var history = new FakeHistoryStore();
        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            var d = Today.AddDays(-dayOffset);
            for (var i = 0; i < 3; i++) history.Append(CompletedStanding(d));
        }

        Assert.Equal(5, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Active_day_below_goal_breaks_streak()
    {
        // Today: 3 cycles ✓
        // Yesterday: 3 cycles ✓
        // 2 days ago: ONLY 1 cycle (active but below goal) — should break
        // 3 days ago: 3 cycles ✓ (but unreachable past the break)
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-1)));
        history.Append(CompletedStanding(Today.AddDays(-2))); // active but only 1
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-3)));

        Assert.Equal(2, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Paused_day_no_records_does_not_break_streak()
    {
        // Today: 3 ✓
        // Yesterday: 3 ✓
        // 2 days ago: nothing (machine off / app paused) — skipped, not a miss
        // 3 days ago: 3 ✓ — should still be reachable
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-1)));
        // no records 2 days ago
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-3)));

        Assert.Equal(3, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Sitting_only_day_is_active_and_below_goal_breaks_streak()
    {
        // Today: 3 ✓
        // Yesterday: only sitting records (active day, but zero standing cycles) — breaks
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today));
        history.Append(Sitting(Today.AddDays(-1)));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-2)));

        Assert.Equal(1, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Toggled_standing_cycles_do_not_count_toward_goal()
    {
        // 3 toggled standing cycles do NOT meet the goal; only CompletedNaturally counts.
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(ToggledStanding(Today));

        Assert.Equal(0, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Cycle_attributed_by_start_date_not_end_date()
    {
        // A standing cycle that starts at 11:55 PM on day D and ends at 12:25 AM
        // on day D+1 should count for day D (StartedAt), not D+1 (EndedAt).
        var history = new FakeHistoryStore();
        var yesterday = Today.AddDays(-1);
        for (var i = 0; i < 2; i++) history.Append(CompletedStanding(yesterday));
        history.Append(new CycleRecord
        {
            StartedAt = yesterday.ToDateTime(new TimeOnly(23, 55)),
            EndedAt = Today.ToDateTime(new TimeOnly(0, 25)),
            WasStanding = true,
            Outcome = CycleOutcome.CompletedNaturally,
            ActualDurationSeconds = 30 * 60,
            PlannedDurationSeconds = 30 * 60
        });

        Assert.Equal(1, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Streak_caps_at_year_lookback()
    {
        var history = new FakeHistoryStore();
        // Hit goal every day for 400 days.
        for (var i = 0; i < 400; i++)
        {
            var d = Today.AddDays(-i);
            for (var c = 0; c < 3; c++) history.Append(CompletedStanding(d));
        }

        var streak = StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero);
        Assert.InRange(streak, 1, 366);
    }

    [Fact]
    public void Multiple_completions_in_one_day_each_count_separately()
    {
        // Goal of 3. Six standing cycles in a single day = two completions.
        var history = new FakeHistoryStore();
        for (var i = 0; i < 6; i++) history.Append(CompletedStanding(Today));

        Assert.Equal(2, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Completions_accumulate_across_days_until_kill()
    {
        // Today: 6 → 2 completions. Yesterday: 3 → 1 completion. Total = 3.
        var history = new FakeHistoryStore();
        for (var i = 0; i < 6; i++) history.Append(CompletedStanding(Today));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-1)));

        Assert.Equal(3, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void Active_day_below_goal_in_the_past_resets_the_count()
    {
        // Today 6 (2 completions), yesterday 3 (1 completion), 2-days-ago 1
        // standing cycle (active, zero completions) — kill. Streak counts only
        // today + yesterday.
        var history = new FakeHistoryStore();
        for (var i = 0; i < 6; i++) history.Append(CompletedStanding(Today));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-1)));
        history.Append(CompletedStanding(Today.AddDays(-2)));
        // Older completions are now unreachable.
        for (var i = 0; i < 9; i++) history.Append(CompletedStanding(Today.AddDays(-3)));

        Assert.Equal(3, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }

    [Fact]
    public void RolloverReset_only_day_does_not_break_streak()
    {
        // Today: 3 ✓
        // Yesterday: 3 ✓
        // 2 days ago: only a single RolloverReset record (app punched the
        //             cycle at day-rollover; user wasn't actively interacting).
        //             Must be treated as a paused day, NOT active-but-missed.
        // 3 days ago: 3 ✓ — should still chain through the rollover day.
        var history = new FakeHistoryStore();
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today));
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-1)));
        history.Append(new CycleRecord
        {
            StartedAt = Today.AddDays(-2).ToDateTime(new TimeOnly(23, 30)),
            EndedAt = Today.AddDays(-2).ToDateTime(new TimeOnly(23, 30)),
            WasStanding = false,
            Outcome = CycleOutcome.RolloverReset,
            ActualDurationSeconds = 0,
            PlannedDurationSeconds = 30 * 60
        });
        for (var i = 0; i < 3; i++) history.Append(CompletedStanding(Today.AddDays(-3)));

        // Three goal-met days (Today, -1, -3). The rollover day at -2 is
        // treated as paused — it preserves continuity to -3 but doesn't add
        // to the count, matching the existing "paused day" semantics.
        Assert.Equal(3, StreakCalculator.CalculateCurrent(history, 3, Today, TimeSpan.Zero));
    }
}
