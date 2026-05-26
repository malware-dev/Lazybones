using System;
using System.Collections.Generic;

namespace Lazybones.Features.History;

public interface IHistoryStore
{
    void Append(CycleRecord record);
    IReadOnlyList<CycleRecord> GetDay(DateOnly date);

    /// <summary>
    /// Returns records whose end date falls within [from, to] — both bounds inclusive.
    /// </summary>
    IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to);
    IReadOnlyList<CycleRecord> GetAll();

    /// <summary>
    /// Sum of standing minutes attributed by EndedAt date — minutes accrue on the day
    /// they actually happened. A cycle that started at 11:55 PM and ended at 12:25 AM
    /// contributes its full minutes to the next day.
    /// </summary>
    int StandingMinutesOn(DateOnly day);

    /// <summary>
    /// Count of standing cycles that ran to natural completion, attributed by StartedAt
    /// date — credit goes to the day the user committed to standing. A cycle that started
    /// at 11:55 PM and ended at 12:25 AM counts toward the day it started.
    /// </summary>
    int CompletedStandingCyclesOn(DateOnly day);
}
