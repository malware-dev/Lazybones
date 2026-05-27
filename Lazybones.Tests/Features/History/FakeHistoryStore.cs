using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Features.History;

namespace Lazybones.Tests.Features.History;

/// <summary>
/// In-memory IHistoryStore for tests — no file I/O, no clock dependency.
/// </summary>
internal sealed class FakeHistoryStore : IHistoryStore
{
    private readonly List<CycleRecord> _records = new();

    public void Append(CycleRecord record) => _records.Add(record);

    public IReadOnlyList<CycleRecord> GetDay(DateOnly date) =>
        _records.Where(r => DateOnly.FromDateTime(r.EndedAt) == date).ToList();

    public IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to) =>
        _records.Where(r =>
        {
            var d = DateOnly.FromDateTime(r.EndedAt);
            return d >= from && d <= to;
        }).ToList();

    public IReadOnlyList<CycleRecord> GetAll() => _records.ToList();

    public int StandingMinutesOn(DateOnly day) =>
        _records
            .Where(r => r.WasStanding && DateOnly.FromDateTime(r.EndedAt) == day)
            .Sum(r => r.ActualDurationSeconds) / 60;

    public int CompletedStandingCyclesOn(DateOnly day) =>
        _records.Count(r => r.WasStanding
                            && r.Outcome == CycleOutcome.CompletedNaturally
                            && !r.WasTimeEdited
                            && DateOnly.FromDateTime(r.StartedAt) == day);
}
