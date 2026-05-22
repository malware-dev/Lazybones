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
    int GetTodayStandingMinutes();
    int GetTodayStandingCycles();
}
