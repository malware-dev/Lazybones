using System;
using System.Collections.Generic;
using System.Linq;
using Lazybones.Models;
using Lazybones.Services;

namespace Lazybones.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly IHistoryStore _history;
    private readonly Action _onDailyGoalChanged;

    public DashboardViewModel(AppState state, IHistoryStore history, Action onDailyGoalChanged)
    {
        _state = state;
        _history = history;
        _onDailyGoalChanged = onDailyGoalChanged;

        Achievements = AchievementCatalog.All
            .Select(a => new AchievementViewItem(a, _state.UnlockedAchievementIds.Contains(a.Id)))
            .ToList();

        HeatmapData = BuildHeatmap();
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

    public int DailyGoalMinutes
    {
        get => _state.DailyGoalMinutes;
        set
        {
            if (_state.DailyGoalMinutes == value) return;
            _state.DailyGoalMinutes = value;
            _state.SaveState();
            OnPropertyChanged(nameof(DailyGoalMinutes));
            OnPropertyChanged(nameof(TodayProgressText));
            _onDailyGoalChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _state.StartWithWindows;
        set
        {
            if (_state.StartWithWindows == value) return;
            _state.StartWithWindows = value;
            StartupService.Instance.SetEnabled(value);
            _state.SaveState();
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }

    public string StartWithOsLabel => StartupService.LoginItemLabel;

    public int TodayStandingMinutes => _history.GetTodayStandingMinutes();

    public string TodayProgressText => $"{TodayStandingMinutes} / {DailyGoalMinutes} min";

    public int CurrentStreak => StreakCalculator.CalculateCurrent(
        _history, _state.DailyGoalMinutes, DateOnly.FromDateTime(DateTime.Now));

    public IReadOnlyDictionary<DateOnly, int> HeatmapData { get; }

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
