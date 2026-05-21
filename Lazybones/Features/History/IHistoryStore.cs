using System;
using System.Collections.Generic;

namespace Lazybones.Features.History;

public interface IHistoryStore
{
    void Append(CycleRecord record);
    IReadOnlyList<CycleRecord> GetDay(DateOnly date);
    IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to);
    IReadOnlyList<CycleRecord> GetAll();
    int GetTodayStandingMinutes();
    int GetTodayStandingCycles();
}
