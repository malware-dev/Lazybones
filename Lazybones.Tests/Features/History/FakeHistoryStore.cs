using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Core.State;
using Lazybones.Features.History;

namespace Lazybones.Tests.Features.History;

/// <summary>
/// In-memory IHistoryStore for tests — no file I/O, no clock dependency.
/// </summary>
internal sealed class FakeHistoryStore : IHistoryStore
{
    private readonly List<CycleRecord> _records = new();

    public void Append(CycleRecord record) => _records.Add(record);

    public IReadOnlyList<CycleRecord> GetDay(DateOnly date, TimeSpan rolloverTime) =>
        _records.Where(r => LogicalDay.From(r.EndedAt, rolloverTime) == date).ToList();

    public IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to, TimeSpan rolloverTime) =>
        _records.Where(r =>
        {
            var d = LogicalDay.From(r.EndedAt, rolloverTime);
            return d >= from && d <= to;
        }).ToList();

    public IReadOnlyList<CycleRecord> GetAll() => _records.ToList();

    public int StandingMinutesOn(DateOnly day, TimeSpan rolloverTime) =>
        _records
            .Where(r => r.WasStanding && LogicalDay.From(r.EndedAt, rolloverTime) == day)
            .Sum(r => r.ActualDurationSeconds) / 60;

    public int CompletedStandingCyclesOn(DateOnly day, TimeSpan rolloverTime) =>
        _records.Count(r => r.WasStanding
                            && r.Outcome == CycleOutcome.CompletedNaturally
                            && !r.WasTimeEdited
                            && LogicalDay.From(r.StartedAt, rolloverTime) == day);
}
