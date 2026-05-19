using System.Collections.Generic;

namespace Lazybones.Models;

public sealed record Achievement(string Id, string Title, string Description);

public static class AchievementCatalog
{
    public const string FirstStandId = "first_stand";
    public const string QuickDrawId = "quick_draw";
    public const string IronLegsId = "iron_legs";
    public const string EarlyBirdId = "early_bird";
    public const string NightOwlId = "night_owl";

    public const string WarmingUpId = "warming_up";
    public const string SevenDayStreakId = "seven_day_streak";
    public const string TwoWeekWonderId = "two_week_wonder";
    public const string HabitFormedId = "habit_formed";

    public const string DailyDriverId = "daily_driver";
    public const string OverachieverId = "overachiever";
    public const string DoubleDownId = "double_down";
    public const string PerfectDayId = "perfect_day";

    public const string CenturionId = "centurion";
    public const string LongHaulId = "long_haul";
    public const string MountaineerId = "mountaineer";

    public static IReadOnlyList<Achievement> All { get; } = new[]
    {
        new Achievement(FirstStandId, "First Stand", "You completed your first standing cycle. Welcome!"),
        new Achievement(QuickDrawId, "Quick Draw", "Responded to a prompt within ten seconds."),
        new Achievement(IronLegsId, "Iron Legs", "Stood through a 30+ minute cycle without bailing."),
        new Achievement(EarlyBirdId, "Early Bird", "Finished a standing cycle that started before 9 AM."),
        new Achievement(NightOwlId, "Night Owl", "Finished a standing cycle past 10 PM."),

        new Achievement(WarmingUpId, "Warming Up", "Three days in a row at goal. The habit begins."),
        new Achievement(SevenDayStreakId, "7-Day Streak", "Seven days of hitting your daily goal in a row."),
        new Achievement(TwoWeekWonderId, "Two-Week Wonder", "Fourteen straight days. You're not playing."),
        new Achievement(HabitFormedId, "Habit Formed", "Thirty days. Whatever this was, it's a habit now."),

        new Achievement(DailyDriverId, "Daily Driver", "Completed five standing cycles in one day."),
        new Achievement(OverachieverId, "Overachiever", "Stood for 1.5× your daily goal in a single day."),
        new Achievement(DoubleDownId, "Double Down", "Doubled your daily goal in a single day."),
        new Achievement(PerfectDayId, "Perfect Day", "Hit your daily goal without dismissing a single prompt."),

        new Achievement(CenturionId, "Centurion", "Completed 100 standing cycles."),
        new Achievement(LongHaulId, "Long Haul", "Ten cumulative hours of standing. Your back thanks you."),
        new Achievement(MountaineerId, "Mountaineer", "One hundred cumulative hours of standing.")
    };
}
