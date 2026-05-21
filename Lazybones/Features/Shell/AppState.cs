using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lazybones.Features.Shell;

public class AppState
{
    public static AppState LoadState()
    {
        var filePath = GetFilePath();

        // If the file doesn't exist, return a new instance of AppState
        if (!File.Exists(filePath))
            return new AppState();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState) ?? new AppState();
        }
        catch (Exception)
        {
            // If there was an error reading the file, return a new instance of AppState
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

    public void SaveState()
    {
        var filePath = GetFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
        var json = JsonSerializer.Serialize(this, AppStateJsonContext.Default.AppState);

        // Write to a temp file then atomically replace, so a crash mid-write
        // can't leave a half-written state.json that fails to deserialize.
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, json);
        if (File.Exists(filePath))
            File.Replace(tempPath, filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, filePath);
    }

    private static string GetFilePath()
    {
        return Path.Combine(GetDataDir(), "state.json");
    }

    internal static string GetDataDir()
    {
        var custom = Environment.GetEnvironmentVariable("LAZYBONES_DATA_DIR");
        if (!string.IsNullOrEmpty(custom)) return custom;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var newDir = Path.Combine(appData, "Malforge", "Lazybones");
        var oldDir = Path.Combine(appData, "Malforge", "StandUp");

        // One-time migration from the project's previous name. If the new
        // folder doesn't exist yet but the old one does, move it. After this
        // succeeds once, subsequent runs ignore the old path entirely.
        if (!Directory.Exists(newDir) && Directory.Exists(oldDir))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newDir) ?? string.Empty);
                Directory.Move(oldDir, newDir);
            }
            catch
            {
                // If the move fails (e.g. permissions, file locked) fall back
                // to the old location so the user doesn't lose access to their
                // history. The next launch will try again.
                return oldDir;
            }
        }

        return newDir;
    }
}

[JsonSerializable(typeof(AppState))]
internal partial class AppStateJsonContext : JsonSerializerContext;