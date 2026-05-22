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

public class DashboardViewModel : ViewModelBase
{
    public const int UpdatesTabIndex = 3;

    private readonly AppState _state;
    private readonly IHistoryStore _history;
    private readonly Action _onDailyGoalChanged;
    private readonly UpdateService _updates = UpdateService.Instance;
    private int _selectedTabIndex;

    public DashboardViewModel(AppState state, IHistoryStore history, Action onDailyGoalChanged)
    {
        _state = state;
        _history = history;
        _onDailyGoalChanged = onDailyGoalChanged;

        Achievements = AchievementCatalog.All
            .Select(a => new AchievementViewItem(a, _state.UnlockedAchievementIds.Contains(a.Id)))
            .ToList();

        HeatmapData = BuildHeatmap();
        CyclesPerDay = BuildCyclesPerDay();

        CheckForUpdatesCommand = new RelayCommand(() => _ = _updates.CheckAsync());
        RestartNowCommand = new RelayCommand(_updates.ApplyAndRestart);

        _updates.PropertyChanged += OnUpdateServicePropertyChanged;
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

    public string StartWithOsLabel => StartupService.Instance.LoginItemLabel;

    public int TodayStandingMinutes => _history.GetTodayStandingMinutes();
    public int TodayStandingCycles => _history.GetTodayStandingCycles();

    public string TodayProgressText => $"{TodayStandingCycles} / {DailyCycleGoal} cycles";
    public string TodayMinutesText => $"{TodayStandingMinutes} min stood";

    public int CurrentStreak => StreakCalculator.CalculateCurrent(
        _history, _state.DailyCycleGoal, DateOnly.FromDateTime(DateTime.Now));

    public IReadOnlyDictionary<DateOnly, int> HeatmapData { get; }

    public IReadOnlyList<int> CyclesPerDay { get; }

    public IReadOnlyList<AchievementViewItem> Achievements { get; }

    public int UnlockedCount => Achievements.Count(a => a.IsUnlocked);

    public string UnlockedSummary => $"{UnlockedCount} of {Achievements.Count} unlocked";

    private Dictionary<DateOnly, int> BuildHeatmap()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dow = ((int)today.DayOfWeek + 6) % 7;
        var lastMonday = today.AddDays(-dow);
        var firstMonday = lastMonday.AddDays(-12 * 7);
        var records = _history.GetRange(firstMonday, today);

        var data = new Dictionary<DateOnly, int>();
        foreach (var r in records)
        {
            if (!r.WasStanding) continue;
            var d = DateOnly.FromDateTime(r.EndedAt);
            data[d] = data.GetValueOrDefault(d, 0) + r.ActualDurationSeconds / 60;
        }
        return data;
    }

    private int[] BuildCyclesPerDay()
    {
        const int days = 14;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = today.AddDays(-(days - 1));
        var records = _history.GetRange(start, today);

        var result = new int[days];
        foreach (var r in records)
        {
            if (!r.WasStanding || r.Outcome != CycleOutcome.CompletedNaturally) continue;
            var day = DateOnly.FromDateTime(r.StartedAt);
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
