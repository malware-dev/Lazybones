using System;
using System.IO;
using Lazybones.Core.State;
using Xunit;

namespace Lazybones.Tests.Core.State;

public class AppStateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public AppStateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Lazybones.Tests.AppState", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "state.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void LoadFrom_returns_defaults_when_file_missing()
    {
        var state = AppState.LoadFrom(_filePath);

        Assert.True(state.IsRunning);
        Assert.False(state.IsStanding);
        Assert.Equal(30, state.StandingTimeInMinutes);
        Assert.Equal(120, state.SittingTimeInMinutes);
        Assert.Equal(3, state.DailyCycleGoal);
        Assert.False(state.HasAskedAboutStartup);
        Assert.False(state.StartWithWindows);
        Assert.Empty(state.UnlockedAchievementIds);
        Assert.Null(state.CycleStartedAt);
        Assert.False(state.CurrentCycleTimeEdited);
    }

    [Fact]
    public void LoadFrom_returns_defaults_when_json_corrupt()
    {
        File.WriteAllText(_filePath, "{ not valid json");
        var state = AppState.LoadFrom(_filePath);
        Assert.True(state.IsRunning); // matches the default ctor
    }

    [Fact]
    public void LoadFrom_returns_defaults_when_file_empty()
    {
        File.WriteAllText(_filePath, "");
        var state = AppState.LoadFrom(_filePath);
        Assert.True(state.IsRunning);
    }

    [Fact]
    public void Round_trip_preserves_all_fields()
    {
        var original = new AppState
        {
            Left = 100,
            Top = 200,
            IsRunning = false,
            ElapsedTimeInSeconds = 1234,
            IsStanding = true,
            StandingTimeInMinutes = 25,
            SittingTimeInMinutes = 75,
            DailyCycleGoal = 5,
            HasAskedAboutStartup = true,
            StartWithWindows = true,
            UnlockedAchievementIds = new() { "first_stand", "quick_draw" },
            CycleStartedAt = new DateTime(2026, 5, 26, 14, 0, 0, DateTimeKind.Utc),
            CurrentCycleTimeEdited = true
        };

        original.SaveTo(_filePath);
        var loaded = AppState.LoadFrom(_filePath);

        Assert.Equal(original.Left, loaded.Left);
        Assert.Equal(original.Top, loaded.Top);
        Assert.Equal(original.IsRunning, loaded.IsRunning);
        Assert.Equal(original.ElapsedTimeInSeconds, loaded.ElapsedTimeInSeconds);
        Assert.Equal(original.IsStanding, loaded.IsStanding);
        Assert.Equal(original.StandingTimeInMinutes, loaded.StandingTimeInMinutes);
        Assert.Equal(original.SittingTimeInMinutes, loaded.SittingTimeInMinutes);
        Assert.Equal(original.DailyCycleGoal, loaded.DailyCycleGoal);
        Assert.Equal(original.HasAskedAboutStartup, loaded.HasAskedAboutStartup);
        Assert.Equal(original.StartWithWindows, loaded.StartWithWindows);
        Assert.Equal(original.UnlockedAchievementIds, loaded.UnlockedAchievementIds);
        Assert.Equal(original.CycleStartedAt, loaded.CycleStartedAt);
        Assert.Equal(original.CurrentCycleTimeEdited, loaded.CurrentCycleTimeEdited);
    }

    [Fact]
    public void SaveTo_creates_missing_directories()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "deeper", "state.json");
        new AppState { IsStanding = true }.SaveTo(nestedPath);
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void SaveTo_overwrites_existing_file_atomically_without_temp_leftover()
    {
        new AppState { DailyCycleGoal = 1 }.SaveTo(_filePath);
        new AppState { DailyCycleGoal = 9 }.SaveTo(_filePath);

        var loaded = AppState.LoadFrom(_filePath);
        Assert.Equal(9, loaded.DailyCycleGoal);
        Assert.False(File.Exists(_filePath + ".tmp"));
    }

    [Fact]
    public void ResolveDataDir_uses_custom_override_when_set()
    {
        var custom = Path.Combine(_tempDir, "custom-dir");
        var result = AppState.ResolveDataDir(custom, appDataRoot: "/unused");
        Assert.Equal(custom, result);
    }

    [Fact]
    public void ResolveDataDir_uses_custom_override_even_when_appdata_is_present()
    {
        var custom = Path.Combine(_tempDir, "custom");
        var appData = Path.Combine(_tempDir, "appdata");
        Directory.CreateDirectory(Path.Combine(appData, "Malforge", "Lazybones"));

        Assert.Equal(custom, AppState.ResolveDataDir(custom, appData));
    }

    [Fact]
    public void ResolveDataDir_picks_new_dir_when_neither_exists_yet()
    {
        var appData = Path.Combine(_tempDir, "appdata");
        var newDir = Path.Combine(appData, "Malforge", "Lazybones");
        var result = AppState.ResolveDataDir(null, appData);
        Assert.Equal(newDir, result);
    }

    [Fact]
    public void ResolveDataDir_picks_new_dir_when_new_dir_already_exists_no_migration()
    {
        var appData = Path.Combine(_tempDir, "appdata");
        var newDir = Path.Combine(appData, "Malforge", "Lazybones");
        var oldDir = Path.Combine(appData, "Malforge", "StandUp");
        Directory.CreateDirectory(newDir);
        Directory.CreateDirectory(oldDir);

        var result = AppState.ResolveDataDir(null, appData);

        Assert.Equal(newDir, result);
        Assert.True(Directory.Exists(oldDir), "Old dir must not be touched when new dir already exists.");
    }

    [Fact]
    public void ResolveDataDir_migrates_old_dir_to_new_when_only_old_exists()
    {
        var appData = Path.Combine(_tempDir, "appdata");
        var newDir = Path.Combine(appData, "Malforge", "Lazybones");
        var oldDir = Path.Combine(appData, "Malforge", "StandUp");
        Directory.CreateDirectory(oldDir);
        File.WriteAllText(Path.Combine(oldDir, "history.jsonl"), "marker");

        var result = AppState.ResolveDataDir(null, appData);

        Assert.Equal(newDir, result);
        Assert.True(Directory.Exists(newDir));
        Assert.False(Directory.Exists(oldDir));
        Assert.Equal("marker", File.ReadAllText(Path.Combine(newDir, "history.jsonl")));
    }

    [Fact]
    public void ResolveDataDir_empty_override_falls_through_to_appdata_resolution()
    {
        var appData = Path.Combine(_tempDir, "appdata");
        var newDir = Path.Combine(appData, "Malforge", "Lazybones");

        Assert.Equal(newDir, AppState.ResolveDataDir("", appData));
    }
}
