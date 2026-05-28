using System;
using System.IO;
using Lazybones.Features.History;
using Xunit;

namespace Lazybones.Tests.Features.History;

public class HistoryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public HistoryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Lazybones.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "history.jsonl");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private static CycleRecord Standing(DateTime started, int durationMinutes = 30) => new()
    {
        StartedAt = started,
        EndedAt = started.AddMinutes(durationMinutes),
        WasStanding = true,
        Outcome = CycleOutcome.CompletedNaturally,
        ActualDurationSeconds = durationMinutes * 60,
        PlannedDurationSeconds = durationMinutes * 60
    };

    [Fact]
    public void GetAll_empty_when_file_missing()
    {
        var store = new HistoryStore(_filePath);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Append_persists_record_to_disk()
    {
        var store = new HistoryStore(_filePath);
        store.Append(Standing(new DateTime(2026, 5, 26, 10, 0, 0)));

        Assert.True(File.Exists(_filePath));
        var lines = File.ReadAllLines(_filePath);
        Assert.Single(lines);
        Assert.Contains("WasStanding", lines[0]);
    }

    [Fact]
    public void Append_creates_data_directory_if_missing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "deeper", "history.jsonl");
        var store = new HistoryStore(nestedPath);
        store.Append(Standing(new DateTime(2026, 5, 26, 10, 0, 0)));
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Round_trip_preserves_record_fields()
    {
        var original = new CycleRecord
        {
            StartedAt = new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 5, 26, 10, 30, 0, DateTimeKind.Utc),
            WasStanding = true,
            Outcome = CycleOutcome.Toggled,
            ActualDurationSeconds = 20 * 60,
            PlannedDurationSeconds = 30 * 60,
            PromptDismissed = false,
            ResponseDelaySeconds = 5
        };

        new HistoryStore(_filePath).Append(original);

        var loaded = new HistoryStore(_filePath).GetAll();
        Assert.Single(loaded);
        var r = loaded[0];
        Assert.Equal(original.WasStanding, r.WasStanding);
        Assert.Equal(original.Outcome, r.Outcome);
        Assert.Equal(original.ActualDurationSeconds, r.ActualDurationSeconds);
        Assert.Equal(original.PlannedDurationSeconds, r.PlannedDurationSeconds);
        Assert.Equal(original.ResponseDelaySeconds, r.ResponseDelaySeconds);
    }

    [Fact]
    public void Corrupt_line_is_skipped_other_records_load()
    {
        File.WriteAllText(_filePath,
            "{\"StartedAt\":\"2026-05-26T10:00:00\",\"EndedAt\":\"2026-05-26T10:30:00\",\"WasStanding\":true,\"Outcome\":\"CompletedNaturally\",\"ActualDurationSeconds\":1800,\"PlannedDurationSeconds\":1800}\n" +
            "this is not json at all\n" +
            "{\"StartedAt\":\"2026-05-26T11:00:00\",\"EndedAt\":\"2026-05-26T11:30:00\",\"WasStanding\":false,\"Outcome\":\"CompletedNaturally\",\"ActualDurationSeconds\":1800,\"PlannedDurationSeconds\":1800}\n");

        var loaded = new HistoryStore(_filePath).GetAll();
        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public void Blank_lines_are_skipped_without_error()
    {
        File.WriteAllText(_filePath,
            "\n" +
            "{\"StartedAt\":\"2026-05-26T10:00:00\",\"EndedAt\":\"2026-05-26T10:30:00\",\"WasStanding\":true,\"Outcome\":\"CompletedNaturally\",\"ActualDurationSeconds\":1800,\"PlannedDurationSeconds\":1800}\n" +
            "\n");

        Assert.Single(new HistoryStore(_filePath).GetAll());
    }

    [Fact]
    public void GetDay_filters_by_EndedAt()
    {
        var store = new HistoryStore(_filePath);
        var d1 = new DateTime(2026, 5, 25, 10, 0, 0);
        var d2 = new DateTime(2026, 5, 26, 10, 0, 0);
        store.Append(Standing(d1));
        store.Append(Standing(d2));

        var day = new HistoryStore(_filePath).GetDay(new DateOnly(2026, 5, 26), TimeSpan.Zero);
        Assert.Single(day);
        Assert.Equal(DateOnly.FromDateTime(d2), DateOnly.FromDateTime(day[0].EndedAt));
    }

    [Fact]
    public void GetRange_inclusive_on_both_bounds()
    {
        var store = new HistoryStore(_filePath);
        store.Append(Standing(new DateTime(2026, 5, 24, 10, 0, 0)));
        store.Append(Standing(new DateTime(2026, 5, 25, 10, 0, 0)));
        store.Append(Standing(new DateTime(2026, 5, 26, 10, 0, 0)));
        store.Append(Standing(new DateTime(2026, 5, 27, 10, 0, 0)));

        var fresh = new HistoryStore(_filePath);
        var range = fresh.GetRange(new DateOnly(2026, 5, 25), new DateOnly(2026, 5, 26), TimeSpan.Zero);
        Assert.Equal(2, range.Count);
    }

    [Fact]
    public void StandingMinutesOn_sums_only_standing_cycles_on_given_day()
    {
        var store = new HistoryStore(_filePath);
        var day = new DateTime(2026, 5, 26, 10, 0, 0);

        store.Append(Standing(day, durationMinutes: 30));
        store.Append(Standing(day.AddHours(2), durationMinutes: 45));

        // Sitting cycle same day — must not contribute.
        store.Append(new CycleRecord
        {
            StartedAt = day.AddHours(4),
            EndedAt = day.AddHours(6),
            WasStanding = false,
            Outcome = CycleOutcome.CompletedNaturally,
            ActualDurationSeconds = 2 * 3600,
            PlannedDurationSeconds = 2 * 3600
        });

        // Standing cycle on a different day — must not contribute.
        store.Append(Standing(day.AddDays(-1), durationMinutes: 100));

        var minutes = new HistoryStore(_filePath).StandingMinutesOn(new DateOnly(2026, 5, 26), TimeSpan.Zero);
        Assert.Equal(75, minutes);
    }

    [Fact]
    public void CompletedStandingCyclesOn_attributes_by_StartedAt_not_EndedAt()
    {
        var store = new HistoryStore(_filePath);
        // Cycle starts 23:55 on day D, ends 00:25 on day D+1.
        store.Append(new CycleRecord
        {
            StartedAt = new DateTime(2026, 5, 25, 23, 55, 0),
            EndedAt = new DateTime(2026, 5, 26, 0, 25, 0),
            WasStanding = true,
            Outcome = CycleOutcome.CompletedNaturally,
            ActualDurationSeconds = 30 * 60,
            PlannedDurationSeconds = 30 * 60
        });

        var fresh = new HistoryStore(_filePath);
        Assert.Equal(1, fresh.CompletedStandingCyclesOn(new DateOnly(2026, 5, 25), TimeSpan.Zero));
        Assert.Equal(0, fresh.CompletedStandingCyclesOn(new DateOnly(2026, 5, 26), TimeSpan.Zero));
    }

    [Fact]
    public void CompletedStandingCyclesOn_excludes_toggled_and_reset()
    {
        var store = new HistoryStore(_filePath);
        var day = new DateTime(2026, 5, 26, 10, 0, 0);

        store.Append(Standing(day));
        store.Append(new CycleRecord
        {
            StartedAt = day.AddHours(1),
            EndedAt = day.AddHours(1).AddMinutes(10),
            WasStanding = true,
            Outcome = CycleOutcome.Toggled,
            ActualDurationSeconds = 10 * 60,
            PlannedDurationSeconds = 30 * 60
        });
        store.Append(new CycleRecord
        {
            StartedAt = day.AddHours(2),
            EndedAt = day.AddHours(2).AddMinutes(5),
            WasStanding = true,
            Outcome = CycleOutcome.Reset,
            ActualDurationSeconds = 5 * 60,
            PlannedDurationSeconds = 30 * 60
        });

        Assert.Equal(1, new HistoryStore(_filePath).CompletedStandingCyclesOn(new DateOnly(2026, 5, 26), TimeSpan.Zero));
    }
}
