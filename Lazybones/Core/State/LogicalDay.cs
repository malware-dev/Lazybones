using System;

namespace Lazybones.Core.State;

// Bucketing helper for the configurable day-rollover. Every place that
// previously asked "what calendar day does this DateTime fall on?" now asks
// "what logical day does this DateTime fall on, given the user's rollover?",
// so cycles before the rollover (e.g. a 02:30 standing cycle with a 06:00
// rollover) attribute to the previous logical day — matching the user's
// mental model of when their day starts.
//
// Passing TimeSpan.Zero degenerates to plain calendar-day behaviour; tests
// and any caller that intentionally wants midnight buckets use that.
public static class LogicalDay
{
    public static DateOnly From(DateTime at, TimeSpan rollover)
        => DateOnly.FromDateTime(at - rollover);
}
