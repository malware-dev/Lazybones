using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lazybones.Core.State;

public class AppState
{
    public static AppState LoadState() => LoadFrom(GetFilePath());

    /// <summary>
    /// Load AppState from an arbitrary file path. Returns a fresh AppState when the file
    /// is missing, malformed JSON, or unreadable due to permissions — startup never blocks
    /// on a corrupt state file.
    /// </summary>
    public static AppState LoadFrom(string filePath)
    {
        if (!File.Exists(filePath))
            return new AppState();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState) ?? new AppState();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppState();
        }
    }

    public int? Left { get; set; }
    public int? Top { get; set; }
    public bool IsRunning { get; set; } = true;
    public int ElapsedTimeInSeconds { get; set; }
    public bool IsStanding { get; set; }
    public int StandingTimeInMinutes { get; set; } = 30;
    public int SittingTimeInMinutes { get; set; } = 120;
    // Number of standing cycles per day that count as hitting the daily goal.
    // Used by the streak, outer ring, and the goal-based achievements. Replaces
    // an older minutes-based "DailyGoalMinutes" field — old state.json files
    // missing this property fall back to the default.
    public int DailyCycleGoal { get; set; } = 3;
    public bool HasAskedAboutStartup { get; set; }
    public bool StartWithWindows { get; set; }
    public List<string> UnlockedAchievementIds { get; set; } = new();

    public void SaveState() => SaveTo(GetFilePath());

    /// <summary>
    /// Save AppState to an arbitrary file path via a temp file + atomic replace so a crash
    /// mid-write can't leave a half-written file that fails to deserialize. Best-effort:
    /// I/O errors are swallowed so a failed save on tick or shutdown won't crash the app.
    /// </summary>
    public void SaveTo(string filePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
            var json = JsonSerializer.Serialize(this, AppStateJsonContext.Default.AppState);

            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // In-memory state remains authoritative for the running session;
            // the next successful save catches up.
        }
    }

    private static string GetFilePath() => Path.Combine(GetDataDir(), "state.json");

    private static readonly Lazy<string> _dataDir = new(() => ResolveDataDir(
        Environment.GetEnvironmentVariable("LAZYBONES_DATA_DIR"),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));

    internal static string GetDataDir() => _dataDir.Value;

    /// <summary>
    /// Pure data-directory resolver — takes its environmental inputs as parameters so it
    /// can be tested directly without poking process-wide state. The Lazy wrapper above
    /// captures real env/SpecialFolder at first call; tests reach this method directly.
    /// </summary>
    internal static string ResolveDataDir(string? customDirOverride, string appDataRoot)
    {
        if (!string.IsNullOrEmpty(customDirOverride)) return customDirOverride;

        var newDir = Path.Combine(appDataRoot, "Malforge", "Lazybones");
        var oldDir = Path.Combine(appDataRoot, "Malforge", "StandUp");

        // One-time migration from the project's previous name. Lazy<T>'s
        // initializer fires exactly once per process under a lock, so the
        // Directory.Move can't race with itself across threads.
        if (!Directory.Exists(newDir) && Directory.Exists(oldDir))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newDir) ?? string.Empty);
                Directory.Move(oldDir, newDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Permissions / file-lock — fall back to the legacy location so the user
                // doesn't lose access to their history. The next launch will try again.
                return oldDir;
            }
        }

        return newDir;
    }
}

[JsonSerializable(typeof(AppState))]
internal partial class AppStateJsonContext : JsonSerializerContext;
