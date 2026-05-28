using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Lazybones.Core.Mvvm;
using Lazybones.Features.Achievements;
using Lazybones.Features.History;
using Lazybones.Core.State;
using Lazybones.Features.StartAtLogin;
using Lazybones.Features.Updates;

namespace Lazybones.Features.Dashboard;

public class DashboardViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    public const int StatsTabIndex = 0;
    public const int AchievementsTabIndex = 1;
    public const int SettingsTabIndex = 2;
    public const int UpdatesTabIndex = 3;

    private readonly AppState _state;
    private readonly IHistoryStore _history;
    private readonly Action _onDailyGoalChanged;
    private readonly Action _onAlwaysOnTopChanged;
    private readonly UpdateService _updates = UpdateService.Instance;
    private int _selectedTabIndex;

    public DashboardViewModel(AppState state, IHistoryStore history, Action onDailyGoalChanged, Action onAlwaysOnTopChanged)
    {
        _state = state;
        _history = history;
        _onDailyGoalChanged = onDailyGoalChanged;
        _onAlwaysOnTopChanged = onAlwaysOnTopChanged;

        Achievements = AchievementCatalog.All
            .Select(a => new AchievementViewItem(a, _state.UnlockedAchievementIds.Contains(a.Id)))
            .ToList();

        HeatmapData = BuildHeatmap();
        CyclesPerDay = BuildCyclesPerDay();

        CheckForUpdatesCommand = new RelayCommand(() => _ = _updates.CheckAsync());
        RestartNowCommand = new RelayCommand(_updates.ApplyAndRestart);

        _updates.PropertyChanged += OnUpdateServicePropertyChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _updates.PropertyChanged -= OnUpdateServicePropertyChanged;
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public string CurrentVersionText => $"v{_updates.CurrentVersion}";

    public string UpdateStatusLabel => _updates.State switch
    {
        UpdateState.UpdateReady => $"Update ready: v{_updates.AvailableVersion}",
        UpdateState.Checking => "Checking for updates",
        UpdateState.Failed => "Update check failed",
        _ => "Status"
    };

    public string UpdateStatusText
    {
        get
        {
            if (!_updates.CanUpdate)
                return "Updates are only available for installed builds — this is a development build.";
            return _updates.State switch
            {
                UpdateState.Idle => "Click \"Check for updates\" to see if a newer version is available.",
                UpdateState.Checking => "Looking for a newer version on GitHub Releases…",
                UpdateState.UpToDate => "You're running the latest version.",
                UpdateState.UpdateReady => $"Version {_updates.AvailableVersion} has been downloaded. It will install on next launch — click \"Restart now\" to install it immediately.",
                UpdateState.Failed => _updates.ErrorMessage ?? "Something went wrong while checking for updates.",
                _ => string.Empty
            };
        }
    }

    public bool HasUpdateReady => _updates.State == UpdateState.UpdateReady;

    public bool CanCheckForUpdates => _updates.CanUpdate && _updates.State != UpdateState.Checking;

    public string ReleaseNotesText => _updates.ReleaseNotesMarkdown ?? "Release notes are loading…";

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand RestartNowCommand { get; }

    private void OnUpdateServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Every UpdateService property change can affect the derived labels, so
        // just re-raise the lot rather than tracking which depends on which.
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(UpdateStatusLabel));
        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(HasUpdateReady));
        OnPropertyChanged(nameof(CanCheckForUpdates));
        OnPropertyChanged(nameof(ReleaseNotesText));
    }

    public int StandingTime
    {
        get => _state.StandingTimeInMinutes;
        set
        {
            if (_state.StandingTimeInMinutes == value) return;
            _state.StandingTimeInMinutes = value;
            _state.SaveState();
            OnPropertyChanged(nameof(StandingTime));
        }
    }

    public int SittingTime
    {
        get => _state.SittingTimeInMinutes;
        set
        {
            if (_state.SittingTimeInMinutes == value) return;
            _state.SittingTimeInMinutes = value;
            _state.SaveState();
            OnPropertyChanged(nameof(SittingTime));
        }
    }

    public int DailyCycleGoal
    {
        get => _state.DailyCycleGoal;
        set
        {
            if (_state.DailyCycleGoal == value) return;
            _state.DailyCycleGoal = value;
            _state.SaveState();
            OnPropertyChanged(nameof(DailyCycleGoal));
            OnPropertyChanged(nameof(TodayProgressText));
            OnPropertyChanged(nameof(DailyMinuteThreshold));
            _onDailyGoalChanged();
        }
    }

    // Two ints decomposed from the persisted TimeSpan, bound to ClockDial's
    // Hour / Minute. RolloverTimeText is the formatted face shown on the
    // dropdown button — must re-raise whenever either component changes.
    public int RolloverHour
    {
        get => _state.DayRolloverTime.Hours;
        set
        {
            var clamped = ((value % 24) + 24) % 24;
            if (_state.DayRolloverTime.Hours == clamped) return;
            _state.DayRolloverTime = new TimeSpan(clamped, _state.DayRolloverTime.Minutes, 0);
            _state.SaveState();
            OnPropertyChanged(nameof(RolloverHour));
            OnPropertyChanged(nameof(RolloverTimeText));
        }
    }

    public int RolloverMinute
    {
        get => _state.DayRolloverTime.Minutes;
        set
        {
            var clamped = ((value % 60) + 60) % 60;
            if (_state.DayRolloverTime.Minutes == clamped) return;
            _state.DayRolloverTime = new TimeSpan(_state.DayRolloverTime.Hours, clamped, 0);
            _state.SaveState();
            OnPropertyChanged(nameof(RolloverMinute));
            OnPropertyChanged(nameof(RolloverTimeText));
        }
    }

    public string RolloverTimeText =>
        $"{_state.DayRolloverTime.Hours:00}:{_state.DayRolloverTime.Minutes:00}";

    // Bound to a ComboBox's SelectedIndex: 0 = seated, 1 = standing. Kept as
    // a derived view over the bool in AppState so persistence semantics stay
    // unchanged.
    public int StartDayModeIndex
    {
        get => _state.StartDayStanding ? 1 : 0;
        set
        {
            var standing = value == 1;
            if (_state.StartDayStanding == standing) return;
            _state.StartDayStanding = standing;
            _state.SaveState();
            OnPropertyChanged(nameof(StartDayModeIndex));
        }
    }

    // Derived from cycle goal × cycle length — the implicit minute equivalent
    // of your daily commitment. The heatmap uses this to colour cells.
    public int DailyMinuteThreshold => _state.DailyCycleGoal * _state.StandingTimeInMinutes;

    public bool StartWithWindows
    {
        get => _state.StartWithWindows;
        set
        {
            if (_state.StartWithWindows == value) return;
            // Persist what the OS actually did, not what the user clicked, so
            // the toggle reflects reality after restart.
            var applied = StartupService.Instance.SetEnabled(value) && value;
            _state.StartWithWindows = applied;
            _state.SaveState();
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }

    public bool AlwaysOnTop
    {
        get => _state.AlwaysOnTop;
        set
        {
            if (_state.AlwaysOnTop == value) return;
            _state.AlwaysOnTop = value;
            _state.SaveState();
            OnPropertyChanged(nameof(AlwaysOnTop));
            _onAlwaysOnTopChanged();
        }
    }

    public string StartWithOsLabel => StartupService.Instance.LoginItemLabel;

    private DateOnly Today => LogicalDay.From(DateTime.Now, _state.DayRolloverTime);

    public int TodayStandingMinutes => _history.StandingMinutesOn(Today, _state.DayRolloverTime);
    public int TodayStandingCycles => _history.CompletedStandingCyclesOn(Today, _state.DayRolloverTime);

    public string TodayProgressText => $"{TodayStandingCycles} / {DailyCycleGoal} cycles";
    public string TodayMinutesText => $"{TodayStandingMinutes} min stood";

    public int CurrentStreak => StreakCalculator.CalculateCurrent(
        _history, _state.DailyCycleGoal, Today, _state.DayRolloverTime);

    public IReadOnlyDictionary<DateOnly, int> HeatmapData { get; }

    public IReadOnlyList<int> CyclesPerDay { get; }

    public IReadOnlyList<AchievementViewItem> Achievements { get; }

    public int UnlockedCount => Achievements.Count(a => a.IsUnlocked);

    public string UnlockedSummary => $"{UnlockedCount} of {Achievements.Count} unlocked";

    private Dictionary<DateOnly, int> BuildHeatmap()
    {
        var rollover = _state.DayRolloverTime;
        var today = LogicalDay.From(DateTime.Now, rollover);
        var dow = ((int)today.DayOfWeek + 6) % 7;
        var lastMonday = today.AddDays(-dow);
        var firstMonday = lastMonday.AddDays(-12 * 7);
        var records = _history.GetRange(firstMonday, today, rollover);

        var data = new Dictionary<DateOnly, int>();
        foreach (var r in records)
        {
            if (!r.WasStanding) continue;
            var d = LogicalDay.From(r.EndedAt, rollover);
            data[d] = data.GetValueOrDefault(d, 0) + r.ActualDurationSeconds / 60;
        }
        return data;
    }

    private int[] BuildCyclesPerDay()
    {
        const int days = 14;
        var rollover = _state.DayRolloverTime;
        var today = LogicalDay.From(DateTime.Now, rollover);
        var start = today.AddDays(-(days - 1));
        var records = _history.GetRange(start, today, rollover);

        var result = new int[days];
        foreach (var r in records)
        {
            if (!r.WasStanding || r.Outcome != CycleOutcome.CompletedNaturally) continue;
            var day = LogicalDay.From(r.StartedAt, rollover);
            var index = day.DayNumber - start.DayNumber;
            if (index >= 0 && index < days) result[index]++;
        }
        return result;
    }
}

public sealed class AchievementViewItem
{
    public AchievementViewItem(Achievement achievement, bool isUnlocked)
    {
        Title = achievement.Title;
        Description = achievement.Description;
        IsUnlocked = isUnlocked;
    }

    public string Title { get; }
    public string Description { get; }
    public bool IsUnlocked { get; }
}
