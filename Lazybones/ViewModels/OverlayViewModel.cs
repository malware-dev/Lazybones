using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Lazybones.Models;

namespace Lazybones.ViewModels;

public class OverlayViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private OverlayType _overlayType;
    private Action<bool>? _callback;
    private int _standingTime;
    private int _sittingTime;
    private int _dailyGoalMinutes;
    private bool _startWithWindows;
    private int _adjustHours;
    private int _adjustMinutes;
    private int _adjustSeconds;
    private string _timeInput = string.Empty;
    private TimeSpan _currentTimeForAdjustment;

    private readonly Queue<Achievement> _pendingAchievements = new();
    private DispatcherTimer? _toastTimer;
    private IReadOnlyDictionary<DateOnly, int>? _heatmapData;

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetField(ref _isVisible, value);
    }

    public string Title
    {
        get => _title;
        private set => SetField(ref _title, value);
    }

    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public OverlayType OverlayType
    {
        get => _overlayType;
        private set => SetField(ref _overlayType, value);
    }

    public int StandingTime
    {
        get => _standingTime;
        set => SetField(ref _standingTime, value);
    }

    public int SittingTime
    {
        get => _sittingTime;
        set => SetField(ref _sittingTime, value);
    }

    public int DailyGoalMinutes
    {
        get => _dailyGoalMinutes;
        set => SetField(ref _dailyGoalMinutes, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetField(ref _startWithWindows, value);
    }

    public int AdjustHours
    {
        get => _adjustHours;
        private set => SetField(ref _adjustHours, value);
    }

    public int AdjustMinutes
    {
        get => _adjustMinutes;
        private set => SetField(ref _adjustMinutes, value);
    }

    public int AdjustSeconds
    {
        get => _adjustSeconds;
        private set => SetField(ref _adjustSeconds, value);
    }

    public string TimeInput
    {
        get => _timeInput;
        set => SetField(ref _timeInput, value);
    }

    public void ShowConfirmation(string title, string message, Action<bool> callback)
    {
        Title = title;
        Message = message;
        OverlayType = OverlayType.Confirmation;
        _callback = callback;
        IsVisible = true;
    }

    public void ShowSettings(int standingTime, int sittingTime, int dailyGoalMinutes, bool startWithWindows,
        Action<bool> callback)
    {
        Title = "Settings";
        StandingTime = standingTime;
        SittingTime = sittingTime;
        DailyGoalMinutes = dailyGoalMinutes;
        StartWithWindows = startWithWindows;
        OverlayType = OverlayType.Settings;
        _callback = callback;
        IsVisible = true;
    }

    public IReadOnlyDictionary<DateOnly, int>? HeatmapData
    {
        get => _heatmapData;
        private set => SetField(ref _heatmapData, value);
    }

    public void ShowHeatmap(IReadOnlyDictionary<DateOnly, int> data, int dailyGoalMinutes, Action<bool> callback)
    {
        Title = "Last 13 weeks";
        HeatmapData = data;
        DailyGoalMinutes = dailyGoalMinutes;
        OverlayType = OverlayType.Heatmap;
        _callback = callback;
        IsVisible = true;
    }

    public void QueueAchievement(Achievement achievement)
    {
        _pendingAchievements.Enqueue(achievement);
        TryShowNextAchievement();
    }

    public void ShowIdleResumed(TimeSpan awayFor)
    {
        // Idle-resume preempts any visible toast or achievement queue. The
        // achievement queue will continue once the idle toast auto-dismisses.
        Title = "Welcome back";
        Message = FormatAwayDuration(awayFor);
        OverlayType = OverlayType.IdleResumeToast;
        IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, OnToastTick);
        _toastTimer.Start();
    }

    private static string FormatAwayDuration(TimeSpan d)
    {
        if (d.TotalHours >= 1)
        {
            var h = (int)d.TotalHours;
            return $"Resumed after {h}h {d.Minutes}m away";
        }
        if (d.TotalMinutes >= 1)
        {
            return $"Resumed after {(int)d.TotalMinutes}m away";
        }
        return "Resumed";
    }

    private void TryShowNextAchievement()
    {
        // Don't interrupt a modal overlay (Settings/Confirmation/TimeAdjustment).
        if (IsVisible && OverlayType != OverlayType.AchievementToast) return;
        if (_pendingAchievements.Count == 0)
        {
            if (OverlayType == OverlayType.AchievementToast) IsVisible = false;
            return;
        }

        var next = _pendingAchievements.Dequeue();
        Title = next.Title;
        Message = next.Description;
        OverlayType = OverlayType.AchievementToast;
        IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, OnToastTick);
        _toastTimer.Start();
    }

    private void OnToastTick(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        _toastTimer = null;
        if (OverlayType == OverlayType.IdleResumeToast)
        {
            IsVisible = false;
            // After the idle toast clears, drain any achievements that may have
            // unlocked while the user was away.
            TryShowNextAchievement();
            return;
        }
        TryShowNextAchievement();
    }

    public void ShowTimeAdjustment(TimeSpan currentTime, Action<bool> callback)
    {
        Title = "Adjust Time";
        AdjustHours = currentTime.Hours;
        AdjustMinutes = currentTime.Minutes;
        AdjustSeconds = currentTime.Seconds;
        TimeInput = $"{currentTime.Hours:00}:{currentTime.Minutes:00}:{currentTime.Seconds:00}";
        _currentTimeForAdjustment = currentTime;
        OverlayType = OverlayType.TimeAdjustment;
        _callback = callback;
        IsVisible = true;
    }

    public void Confirm()
    {
        // Validate time adjustment inputs
        if (OverlayType == OverlayType.TimeAdjustment)
        {
            if (!TimeInputParser.TryParse(TimeInput, _currentTimeForAdjustment, out var time))
            {
                // Invalid input - don't close overlay, just return
                return;
            }

            AdjustHours = (int)time.TotalHours;
            AdjustMinutes = time.Minutes;
            AdjustSeconds = time.Seconds;
        }

        var callback = _callback;
        _callback = null;
        IsVisible = false;

        if (callback != null)
        {
            Dispatcher.UIThread.Post(() => callback(true));
        }

        Dispatcher.UIThread.Post(TryShowNextAchievement, DispatcherPriority.Background);
    }

    public void Cancel()
    {
        var callback = _callback;
        _callback = null;
        IsVisible = false;

        if (callback != null)
        {
            Dispatcher.UIThread.Post(() => callback(false));
        }

        Dispatcher.UIThread.Post(TryShowNextAchievement, DispatcherPriority.Background);
    }
}

public enum OverlayType
{
    None,
    Confirmation,
    Settings,
    TimeAdjustment,
    AchievementToast,
    Heatmap,
    IdleResumeToast
}
