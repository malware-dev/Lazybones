using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lazybones.Models;

namespace Lazybones.Services;

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
            var json = JsonSerializer.Serialize(record, CycleRecordJsonContext.Default.CycleRecord);
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);
            File.AppendAllText(_filePath, json + Environment.NewLine);
            _cache?.Add(record);
        }
    }

    public IReadOnlyList<CycleRecord> GetDay(DateOnly date)
    {
        return LoadAll()
            .Where(r => DateOnly.FromDateTime(r.EndedAt) == date)
            .ToList();
    }

    public IReadOnlyList<CycleRecord> GetRange(DateOnly from, DateOnly to)
    {
        return LoadAll()
            .Where(r =>
            {
                var d = DateOnly.FromDateTime(r.EndedAt);
                return d >= from && d <= to;
            })
            .ToList();
    }

    public IReadOnlyList<CycleRecord> GetAll()
    {
        lock (_lock)
        {
            return LoadAll().ToList();
        }
    }

    public int GetTodayStandingMinutes()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var seconds = LoadAll()
            .Where(r => r.WasStanding && DateOnly.FromDateTime(r.EndedAt) == today)
            .Sum(r => r.ActualDurationSeconds);
        return seconds / 60;
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
                catch
                {
                    // Skip corrupt lines rather than refusing to load the whole file.
                }
            }

            _cache = list;
            return list;
        }
    }

    private static string GetDefaultFilePath()
    {
        return Path.Combine(Models.AppState.GetDataDir(), "history.jsonl");
    }
}
