using System;
using System.Text.Json.Serialization;

namespace Lazybones.Models;

public enum CycleOutcome
{
    CompletedNaturally,
    Toggled,
    Reset
}

public sealed class CycleRecord
{
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public bool WasStanding { get; set; }
    public int PlannedDurationSeconds { get; set; }
    public int ActualDurationSeconds { get; set; }
    public CycleOutcome Outcome { get; set; }

    // True when the cycle expired naturally but the user closed the prompt
    // without choosing (Alt+F4). Only meaningful when Outcome == CompletedNaturally.
    public bool PromptDismissed { get; set; }

    // Seconds between the timer expiring and the user responding to the
    // mode-switch dialog. 0 for user-initiated cycle ends (Toggled/Reset).
    // A long delay (e.g. the app sat overnight on the prompt) indicates the
    // user wasn't engaged with the cycle and disqualifies engagement-based
    // achievements like Iron Legs.
    public int ResponseDelaySeconds { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(CycleRecord))]
internal partial class CycleRecordJsonContext : JsonSerializerContext;
