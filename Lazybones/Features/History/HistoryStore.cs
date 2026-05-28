using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lazybones.Core.State;

namespace Lazybones.Features.History;

public sealed class HistoryStore : IHistoryStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private List<CycleRecord>? _cache;

    public HistoryStore() : this(GetDefaultFilePath()) { }

    public HistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public void Append(CycleRecord record)
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(record, CycleRecordJsonContext.Default.CycleRecord);
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);
                File.AppendAllText(_filePath, json + Environment.NewLine);
                _cache?.Add(record);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort: losing one cycle record is preferable to crashing
                // the app on a transient I/O issue. The in-memory cache is left
                // unchanged so the next successful append will be consistent.
            }
        }
    }

    public IReadOnlyList<CycleRecord> GetDay(DateOnly date, TimeSpan rolloverTime)
    {
        lock (_lock)
        {
            return LoadAll()
                .Where(r => LogicalDay.From(r.EndedAt, rolloverTime) == date)
                .ToList();
        }
    }

    public IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to, TimeSpan rolloverTime)
    {
        lock (_lock)
        {
            return LoadAll()
                .Where(r =>
                {
                    var d = LogicalDay.From(r.EndedAt, rolloverTime);
                    return d >= from && d <= to;
                })
                .ToList();
        }
    }

    public IReadOnlyList<CycleRecord> GetAll()
    {
        lock (_lock)
        {
            return LoadAll().ToList();
        }
    }

    public int StandingMinutesOn(DateOnly day, TimeSpan rolloverTime)
    {
        lock (_lock)
        {
            var seconds = LoadAll()
                .Where(r => r.WasStanding && LogicalDay.From(r.EndedAt, rolloverTime) == day)
                .Sum(r => r.ActualDurationSeconds);
            return seconds / 60;
        }
    }

    public int CompletedStandingCyclesOn(DateOnly day, TimeSpan rolloverTime)
    {
        lock (_lock)
        {
            return LoadAll()
                .Count(r => r.WasStanding
                            && r.Outcome == CycleOutcome.CompletedNaturally
                            && !r.WasTimeEdited
                            && LogicalDay.From(r.StartedAt, rolloverTime) == day);
        }
    }

    private List<CycleRecord> LoadAll()
    {
        lock (_lock)
        {
            if (_cache != null) return _cache;

            var list = new List<CycleRecord>();
            if (!File.Exists(_filePath))
            {
                _cache = list;
                return list;
            }

            foreach (var line in File.ReadAllLines(_filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize(line, CycleRecordJsonContext.Default.CycleRecord);
                    if (record != null) list.Add(record);
                }
                catch (JsonException)
                {
                    // Skip malformed JSON lines rather than refusing to load the whole file.
                    // Disk or permission errors at File.ReadAllLines bubble up; we only
                    // tolerate per-record corruption here.
                }
            }

            _cache = list;
            return list;
        }
    }

    private static string GetDefaultFilePath()
    {
        return Path.Combine(AppState.GetDataDir(), "history.jsonl");
    }
}
