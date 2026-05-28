using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Lazybones.Features.History;

public enum CycleOutcome
{
    CompletedNaturally,
    Toggled,
    Reset,
    // App-initiated reset at the configured day-rollover boundary. Distinct
    // from Reset (which is a user action) so that the streak and the various
    // "did the user interact with the app this day?" checks can ignore it —
    // the rollover is not a short-circuit the user is responsible for.
    RolloverReset
}

public enum PauseReason
{
    ScreenLock,
    ManualPause,
    AppShutdown
}

// A single span of paused time inside a cycle. The cycle's ActualDurationSeconds
// already excludes pause time by construction (EndedAt = StartedAt + actual),
// so these intervals are purely informational — they let us audit a cycle's
// real-time span and explain gaps in user perception ("why did this cycle
// take 8 hours when it was a 45-min stand?").
public sealed class PauseInterval
{
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public PauseReason Reason { get; set; }
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

    // True when the user manually edited the time during this cycle. Tainted
    // cycles do not count toward achievements, the daily-goal ring, or streak
    // progress — but they're still recorded and still contribute to lifetime
    // stats display (minutes stood today, heatmap intensity, etc.).
    public bool WasTimeEdited { get; set; }

    // Every span of paused time that occurred during this cycle. Empty list
    // for cycles with no pauses. Old records (pre-1.1.x) deserialize with the
    // default empty list.
    public List<PauseInterval> Pauses { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(CycleRecord))]
internal partial class CycleRecordJsonContext : JsonSerializerContext;
