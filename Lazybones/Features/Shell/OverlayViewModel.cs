using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Lazybones.Core.Mvvm;
using Lazybones.Features.Achievements;

namespace Lazybones.Features.Shell;

public class OverlayViewModel : ViewModelBase
{
    private bool _isVisible;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private OverlayType _overlayType;
    private Action<bool>? _callback;
    private TimeSpan _adjustedTime;
    private string _timeInput = string.Empty;
    private TimeSpan _currentTimeForAdjustment;

    private readonly Queue<Achievement> _pendingAchievements = new();
    private DispatcherTimer? _toastTimer;

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

    public TimeSpan AdjustedTime
    {
        get => _adjustedTime;
        private set => SetField(ref _adjustedTime, value);
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

    public void ShowDayRollover(string title, string message)
    {
        // Preempts any visible toast or queued achievement, same as the idle
        // toast — a day reset is a foreground event the user should see.
        Title = title;
        Message = message;
        OverlayType = OverlayType.DayRolloverToast;
        IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.Background, OnToastTick);
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
        // Don't interrupt a modal overlay (Confirmation/TimeAdjustment).
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
        if (OverlayType is OverlayType.IdleResumeToast or OverlayType.DayRolloverToast)
        {
            IsVisible = false;
            // After the preempting toast clears, drain any achievements that
            // may have unlocked while it was on screen.
            TryShowNextAchievement();
            return;
        }
        TryShowNextAchievement();
    }

    public void ShowTimeAdjustment(TimeSpan currentTime, Action<bool> callback)
    {
        Title = "Adjust Time";
        AdjustedTime = currentTime;
        TimeInput = $"{currentTime.Hours:00}:{currentTime.Minutes:00}:{currentTime.Seconds:00}";
        _currentTimeForAdjustment = currentTime;
        OverlayType = OverlayType.TimeAdjustment;
        _callback = callback;
        IsVisible = true;
    }

    public void Confirm()
    {
        if (OverlayType == OverlayType.TimeAdjustment)
        {
            if (!TimeInputParser.TryParse(TimeInput, _currentTimeForAdjustment, out var time))
                return;

            AdjustedTime = time;
        }

        Dismiss(accepted: true);
    }

    public void Cancel() => Dismiss(accepted: false);

    private void Dismiss(bool accepted)
    {
        var callback = _callback;
        _callback = null;
        IsVisible = false;

        if (callback != null)
            Dispatcher.UIThread.Post(() => callback(accepted));

        Dispatcher.UIThread.Post(TryShowNextAchievement, DispatcherPriority.Background);
    }
}

public enum OverlayType
{
    None,
    Confirmation,
    TimeAdjustment,
    AchievementToast,
    IdleResumeToast,
    DayRolloverToast
}
