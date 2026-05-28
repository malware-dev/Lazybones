using System;
using System.Collections.Generic;

namespace Lazybones.Features.History;

public interface IHistoryStore
{
    void Append(CycleRecord record);

    /// <summary>
    /// Returns records whose end falls on <paramref name="date"/> in the logical-day
    /// frame defined by <paramref name="rolloverTime"/>. Pass TimeSpan.Zero for
    /// plain calendar-day bucketing.
    /// </summary>
    IReadOnlyList<CycleRecord> GetDay(DateOnly date, TimeSpan rolloverTime);

    /// <summary>
    /// Returns records whose end falls within [from, to] in the logical-day frame
    /// defined by <paramref name="rolloverTime"/>. Both bounds inclusive.
    /// </summary>
    IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to, TimeSpan rolloverTime);

    IReadOnlyList<CycleRecord> GetAll();

    /// <summary>
    /// Sum of standing minutes attributed by EndedAt's logical day — minutes accrue
    /// on the day they actually happened.
    /// </summary>
    int StandingMinutesOn(DateOnly day, TimeSpan rolloverTime);

    /// <summary>
    /// Count of standing cycles that ran to natural completion, attributed by
    /// StartedAt's logical day — credit goes to the logical day the user committed
    /// to standing.
    /// </summary>
    int CompletedStandingCyclesOn(DateOnly day, TimeSpan rolloverTime);
}
