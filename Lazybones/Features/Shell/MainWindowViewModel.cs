using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Lazybones.Core.Mvvm;
using Lazybones.Features.Achievements;
using Lazybones.Features.Dashboard;
using Lazybones.Features.History;
using Lazybones.Features.SessionPresence;
using Lazybones.Features.StartAtLogin;
using Lazybones.Features.Updates;

namespace Lazybones.Features.Shell;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly string[] YouCanSitNowTexts =
    [
        "You can sit now.",
        "Rest your legs.",
        "Sit down and relax.",
        "Sit comfortably.",
        "Take a seat.",
        "Ok, sit.",
        "Time to sit down.",
        "Get your chair.",
        "You can take a break now.",
        "Butt on the chair."
    ];

    private static readonly string[] StandUpNowTexts =
    [
        "Stand up now!",
        "Up, up, up!",
        "Raise your desk!",
        "Get up, get up!",
        "Stand tall!",
        "Time to stand up!",
        "Shift to standing position!",
        "Up, soldier!",
        "Up, lazybones!",
        "Get your a** up!"
    ];

    private string _hours = "00";
    private string _minutes = "00";
    private string _seconds = "00";
    private TimeSpan _time;
    private bool _isStanding;
    private string _text = "Hang on...";

    private readonly Stopwatch _stopwatch = new();
    private bool _isRunning;
    private readonly AppState _state;
    private readonly IHistoryStore _history;
    private Point _windowPosition;
    private readonly OverlayViewModel _overlay = new();
    private readonly DispatcherTimer _timer;
    private bool _triggerInFlight;
    private DateTime _cycleStartedAt;
    private int _cyclePlannedSeconds;
    // Wall-clock time at which the most recent TriggerAsync() fired (= when the
    // timer actually hit zero). Used to measure dialog response time without
    // counting any pre-trigger pause window.
    private DateTime _lastTriggerFiredAt;
    private double _outerRingProgress;
    private double _innerRingProgress;
    private int _streak;
    private DateOnly _currentDay;
    private bool _autoPaused;
    private DateTime _autoPauseStartedAt;
    private readonly IUserPresenceMonitor _presence = UserPresenceMonitor.Create();

    public MainWindowViewModel() : this(new HistoryStore()) { }

    public MainWindowViewModel(IHistoryStore history)
    {
        _history = history;
        _state = AppState.LoadState();

        PlayPauseCommand = new RelayCommand(PlayPause);
        ResetCommand = new RelayCommand(TryReset);
        SwapCommand = new RelayCommand(TryToggle);
        DashboardCommand = new RelayCommand(() => ShowDashboard());
        OpenUpdatesCommand = new RelayCommand(() => ShowDashboard(DashboardViewModel.UpdatesTabIndex));
        AdjustTimeCommand = new RelayCommand(ShowTimeAdjustment);
        ConfirmOverlayCommand = new RelayCommand(() => _overlay.Confirm());
        CancelOverlayCommand = new RelayCommand(() => _overlay.Cancel());

        UpdateService.Instance.PropertyChanged += OnUpdateServicePropertyChanged;

        // Validate position - if minimized (-32000) or off-screen, use default position
        var left = _state.Left ?? 100;
        var top = _state.Top ?? 100;
        if (left < -1000 || top < -1000 || left > 10000 || top > 10000)
        {
            left = 100;
            top = 100;
        }

        WindowPosition = new Point(left, top);
        IsRunning = _state.IsRunning;
        IsStanding = _state.IsStanding;

        // Set text based on mode
        Text = IsStanding
            ? StandUpNowTexts[Random.Shared.Next(StandUpNowTexts.Length)]
            : YouCanSitNowTexts[Random.Shared.Next(YouCanSitNowTexts.Length)];

        // Restore timer from saved state, or use defaults
        if (_state.ElapsedTimeInSeconds > 0)
        {
            Time = TimeSpan.FromSeconds(_state.ElapsedTimeInSeconds);
        }
        else
        {
            Time = TimeSpan.FromMinutes(IsStanding ? _state.StandingTimeInMinutes : _state.SittingTimeInMinutes);
        }

        if (IsRunning)
            _stopwatch.Start();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Normal, OnTimerTick);
        _timer.Start();

        _currentDay = DateOnly.FromDateTime(DateTime.Now);
        StartNewCycle();
        RefreshOuterRing();
        RefreshInnerRing();
        RefreshStreak();

        _presence.Locked += OnScreenLocked;
        _presence.Unlocked += OnScreenUnlocked;

        if (!_state.HasAskedAboutStartup)
            Dispatcher.UIThread.Post(PromptStartWithWindows, DispatcherPriority.Background);
    }

    private void StartNewCycle()
    {
        _cycleStartedAt = DateTime.Now;
        _cyclePlannedSeconds = (IsStanding ? _state.StandingTimeInMinutes : _state.SittingTimeInMinutes) * 60;
        RefreshInnerRing();

        // Piggy-back update checks onto cycle starts: covers startup (initial
        // cycle), every natural trigger, manual Swap, and Reset — i.e., every
        // moment the user is actively engaging with the app. UpdateService
        // throttles repeated calls so a Swap-mashing user can't spam GitHub.
        UpdateService.Instance.RequestBackgroundCheck();
    }

    private void RecordCurrentCycle(CycleOutcome outcome, bool promptDismissed = false)
    {
        // Clamp elapsed against planned so a user-driven Time adjustment can't
        // produce negative or absurd actual-duration values.
        var remaining = (int)Math.Max(0, Time.TotalSeconds);
        var actual = Math.Clamp(_cyclePlannedSeconds - remaining, 0, _cyclePlannedSeconds);

        // Logical end time = StartedAt + active time. By construction this
        // excludes all pause windows (locked, manual, dialog wait), so pause
        // time never shifts day buckets, hour-of-day achievements, or any
        // other time-derived metric.
        var endedAt = _cycleStartedAt.AddSeconds(actual);

        int responseDelaySeconds;
        if (outcome == CycleOutcome.CompletedNaturally)
        {
            // Time between the dialog actually firing (wall clock, captured in
            // Trigger) and the user closing it. Pause time before the trigger
            // doesn't count.
            responseDelaySeconds = (int)Math.Max(0, (DateTime.Now - _lastTriggerFiredAt).TotalSeconds);
        }
        else
        {
            responseDelaySeconds = 0;
        }

        var record = new CycleRecord
        {
            StartedAt = _cycleStartedAt,
            EndedAt = endedAt,
            WasStanding = IsStanding,
            PlannedDurationSeconds = _cyclePlannedSeconds,
            ActualDurationSeconds = actual,
            Outcome = outcome,
            PromptDismissed = promptDismissed,
            ResponseDelaySeconds = responseDelaySeconds
        };
        _history.Append(record);

        RefreshOuterRing();
        RefreshStreak();
        EvaluateAchievements(record);
    }

    private void EvaluateAchievements(CycleRecord record)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var newly = AchievementRules.EvaluateNewlyUnlocked(
            record, _history, _state.DailyCycleGoal, _state.UnlockedAchievementIds, today);
        if (newly.Count == 0) return;

        foreach (var ach in newly)
        {
            _state.UnlockedAchievementIds.Add(ach.Id);
            _overlay.QueueAchievement(ach);
        }
        _state.SaveState();
    }

    private void RefreshOuterRing()
    {
        var goal = Math.Max(1, _state.DailyCycleGoal);
        OuterRingProgress = Math.Min(1.0, (double)_history.GetTodayStandingCycles() / goal);
    }

    private void RefreshStreak()
    {
        Streak = StreakCalculator.CalculateCurrent(_history, _state.DailyCycleGoal, DateOnly.FromDateTime(DateTime.Now));
    }

    public int Streak
    {
        get => _streak;
        private set
        {
            if (SetField(ref _streak, value))
                OnPropertyChanged(nameof(HasStreak));
        }
    }

    public bool HasStreak => _streak > 0;

    private void RefreshInnerRing()
    {
        if (_cyclePlannedSeconds <= 0)
        {
            InnerRingProgress = 0;
            return;
        }
        var remaining = Math.Max(0, Time.TotalSeconds);
        InnerRingProgress = Math.Clamp(1.0 - remaining / _cyclePlannedSeconds, 0, 1);
    }

    public double OuterRingProgress
    {
        get => _outerRingProgress;
        private set => SetField(ref _outerRingProgress, value);
    }

    public double InnerRingProgress
    {
        get => _innerRingProgress;
        private set => SetField(ref _innerRingProgress, value);
    }

    private void PromptStartWithWindows()
    {
        _overlay.ShowConfirmation(
            $"{StartupService.Instance.LoginItemLabel}?",
            "Would you like Get Up, Lazybones! to start automatically when you log in?",
            result =>
            {
                _state.HasAskedAboutStartup = true;
                _state.StartWithWindows = StartupService.Instance.SetEnabled(result) && result;
                _state.SaveState();
            });
    }

    public ICommand PlayPauseCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SwapCommand { get; }
    public ICommand DashboardCommand { get; }
    public ICommand OpenUpdatesCommand { get; }
    public ICommand AdjustTimeCommand { get; }
    public ICommand ConfirmOverlayCommand { get; }
    public ICommand CancelOverlayCommand { get; }

    public bool HasUpdate => UpdateService.Instance.State == UpdateState.UpdateReady;

    public string AvailableVersionText => $"v{UpdateService.Instance.AvailableVersion}";

    private void OnUpdateServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdateService.State))
            OnPropertyChanged(nameof(HasUpdate));
        if (e.PropertyName == nameof(UpdateService.AvailableVersion))
            OnPropertyChanged(nameof(AvailableVersionText));
    }

    public OverlayViewModel Overlay => _overlay;

    public string Hours
    {
        get => _hours;
        private set => SetField(ref _hours, value);
    }

    public string Minutes
    {
        get => _minutes;
        private set => SetField(ref _minutes, value);
    }

    public string Seconds
    {
        get => _seconds;
        private set => SetField(ref _seconds, value);
    }

    public TimeSpan Time
    {
        get => _time;
        set
        {
            if (_time == value) return;
            _time = value;
            // Use total components so values >= 24h render correctly.
            var clamped = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            Hours = ((int)clamped.TotalHours).ToString("00");
            Minutes = clamped.Minutes.ToString("00");
            Seconds = clamped.Seconds.ToString("00");
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    public bool IsStanding
    {
        get => _isStanding;
        private set => SetField(ref _isStanding, value);
    }

    public string Text
    {
        get => _text;
        private set => SetField(ref _text, value);
    }

    public Point WindowPosition
    {
        get => _windowPosition;
        set => SetField(ref _windowPosition, value);
    }

    private void PlayPause()
    {
        if (IsRunning)
            Pause();
        else
            Resume();
    }

    private void Resume()
    {
        if (IsRunning) return;

        // Restart, not Start: Stopwatch.Start() resumes from the previously
        // stored Elapsed value, which would get subtracted from Time on the
        // next tick.
        _stopwatch.Restart();
        Text = IsStanding
            ? StandUpNowTexts[Random.Shared.Next(StandUpNowTexts.Length)]
            : YouCanSitNowTexts[Random.Shared.Next(YouCanSitNowTexts.Length)];
        IsRunning = true;
    }

    private void Pause(string? pauseText = "Paused...")
    {
        if (!IsRunning) return;
        // Reset (not Stop) so any residual elapsed is discarded; otherwise
        // Resume()'s next tick subtracts that residual from Time.
        _stopwatch.Reset();
        if (pauseText != null)
            Text = pauseText;
        IsRunning = false;
    }

    private void Reset()
    {
        ResetStopwatch();
        Time = IsStanding
            ? TimeSpan.FromMinutes(_state.StandingTimeInMinutes)
            : TimeSpan.FromMinutes(_state.SittingTimeInMinutes);
        StartNewCycle();
    }

    private void StandUp()
    {
        Text = StandUpNowTexts[Random.Shared.Next(StandUpNowTexts.Length)];
        Time = TimeSpan.FromMinutes(_state.StandingTimeInMinutes);
        ResetStopwatch();
        IsStanding = true;
        StartNewCycle();
    }

    private void SitDown()
    {
        Text = YouCanSitNowTexts[Random.Shared.Next(YouCanSitNowTexts.Length)];
        Time = TimeSpan.FromMinutes(_state.SittingTimeInMinutes);
        ResetStopwatch();
        IsStanding = false;
        StartNewCycle();
    }

    private void Toggle()
    {
        if (IsStanding)
            SitDown();
        else
            StandUp();
    }

    private async Task TriggerAsync()
    {
        // Re-entrancy guard: a slow dialog open shouldn't allow a second
        // Trigger to fire from a stale Time<=0 condition.
        if (_triggerInFlight) return;
        _triggerInFlight = true;
        // Snapshot the moment the cycle naturally ended (timer hit zero).
        // ResponseDelaySeconds is measured from here, not from the cycle's
        // logical end time, so pre-trigger pauses don't masquerade as a slow
        // response.
        _lastTriggerFiredAt = DateTime.Now;
        try
        {
            Pause();

            var dialog = new ModeSwitchDialog();
            var randomText = IsStanding
                ? YouCanSitNowTexts[Random.Shared.Next(YouCanSitNowTexts.Length)]
                : StandUpNowTexts[Random.Shared.Next(StandUpNowTexts.Length)];
            dialog.SetMessage(randomText);

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow != null)
            {
                await dialog.ShowDialog(mainWindow);
            }

            var promptDismissed = dialog.UserChoice != ModeSwitchDialog.Choice.StartNow
                                  && dialog.UserChoice != ModeSwitchDialog.Choice.Dismiss;
            RecordCurrentCycle(CycleOutcome.CompletedNaturally, promptDismissed);

            switch (dialog.UserChoice)
            {
                case ModeSwitchDialog.Choice.StartNow:
                    Toggle();
                    Resume();
                    break;
                case ModeSwitchDialog.Choice.Dismiss:
                    Toggle();
                    break;
                default:
                    // Closed without choosing (e.g. Alt+F4): keep current mode,
                    // but reset Time to the current mode's default so the timer
                    // doesn't immediately re-trigger when the user resumes.
                    Time = IsStanding
                        ? TimeSpan.FromMinutes(_state.StandingTimeInMinutes)
                        : TimeSpan.FromMinutes(_state.SittingTimeInMinutes);
                    StartNewCycle();
                    break;
            }
        }
        finally
        {
            _triggerInFlight = false;
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _presence.Locked -= OnScreenLocked;
        _presence.Unlocked -= OnScreenUnlocked;
        _presence.Dispose();

        _state.Left = (int)WindowPosition.X;
        _state.Top = (int)WindowPosition.Y;
        _state.ElapsedTimeInSeconds = (int)Time.TotalSeconds;
        _state.IsRunning = IsRunning;
        _state.IsStanding = IsStanding;
        _state.SaveState();
    }

    private void TryToggle()
    {
        var message = IsStanding
            ? "Swap to sitting?"
            : "Swap to standing?";

        _overlay.ShowConfirmation("Swap", message, result =>
        {
            if (!result) return;
            RecordCurrentCycle(CycleOutcome.Toggled);
            Toggle();
        });
    }

    private void TryReset()
    {
        _overlay.ShowConfirmation("Reset", "Reset the timer?", result =>
        {
            if (!result) return;
            RecordCurrentCycle(CycleOutcome.Reset);
            Reset();
        });
    }

    private DashboardWindow? _dashboard;

    private void ShowDashboard(int initialTabIndex = 0)
    {
        if (_dashboard != null)
        {
            if (_dashboard.DataContext is DashboardViewModel existingVm)
                existingVm.SelectedTabIndex = initialTabIndex;
            // Restore if the user minimized via taskbar / OS shortcut — without
            // this, Activate() on a minimized window can leave it in the tray
            // and the cog button appears to do nothing.
            if (_dashboard.WindowState == Avalonia.Controls.WindowState.Minimized)
                _dashboard.WindowState = Avalonia.Controls.WindowState.Normal;
            _dashboard.Activate();
            return;
        }

        var vm = new DashboardViewModel(_state, _history, RefreshOuterRing)
        {
            SelectedTabIndex = initialTabIndex
        };
        _dashboard = new DashboardWindow { DataContext = vm };
        _dashboard.Closed += (_, _) => _dashboard = null;

        var owner = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (owner != null)
            _dashboard.Show(owner);
        else
            _dashboard.Show();
    }

    private void ShowTimeAdjustment()
    {
        _overlay.ShowTimeAdjustment(Time, result =>
        {
            if (result)
            {
                Time = _overlay.AdjustedTime;
                ResetStopwatch();
            }
        });
    }

    private void ResetStopwatch()
    {
        // Keep the stopwatch's running-state in sync with IsRunning so that
        // elapsed time can't accumulate while we're paused (which would then
        // be subtracted from Time on the next Resume).
        if (IsRunning)
            _stopwatch.Restart();
        else
            _stopwatch.Reset();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != _currentDay)
        {
            _currentDay = today;
            RefreshOuterRing();
            RefreshStreak();
        }

        if (!IsRunning) return;
        Time -= _stopwatch.Elapsed;
        _stopwatch.Restart();
        RefreshInnerRing();
        if (Time <= TimeSpan.Zero) _ = TriggerAsync();
    }

    private void OnScreenLocked(object? sender, EventArgs e)
    {
        // Only auto-pause if a cycle is actually running and we aren't already
        // in the middle of handling an end-of-cycle dialog.
        if (_triggerInFlight) return;
        if (!IsRunning) return;

        _autoPauseStartedAt = DateTime.Now;
        _autoPaused = true;
        Pause("Locked...");
    }

    private void OnScreenUnlocked(object? sender, EventArgs e)
    {
        if (!_autoPaused) return;
        _autoPaused = false;

        // If the user manually resumed in between (e.g. via another input
        // device that didn't fully unlock) skip the toast.
        if (IsRunning) return;

        var awayFor = DateTime.Now - _autoPauseStartedAt;
        Resume();
        _overlay.ShowIdleResumed(awayFor);
    }
}
