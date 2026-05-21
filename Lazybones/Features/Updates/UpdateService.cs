using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using Velopack;
using Velopack.Sources;

namespace Lazybones.Features.Updates;

public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    UpdateReady,
    Failed
}

/// <summary>
/// Singleton wrapper around Velopack's UpdateManager. Holds observable state
/// (current/available version, status, release notes) for ViewModels to bind to.
/// All PropertyChanged events are marshalled to the UI thread so background
/// update checks don't break bindings.
/// </summary>
public sealed class UpdateService : INotifyPropertyChanged
{
    public static UpdateService Instance { get; } = new();

    private const string GithubOwner = "malware-dev";
    private const string GithubRepo = "Lazybones";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Lazybones-Updater");
        return http;
    }

    private static readonly TimeSpan BackgroundCheckInterval = TimeSpan.FromMinutes(30);

    private readonly UpdateManager _mgr;
    private UpdateInfo? _pendingUpdate;
    private DateTime _lastBackgroundCheck = DateTime.MinValue;

    private UpdateState _state = UpdateState.Idle;
    private string _currentVersion = "0.0.0";
    private string? _availableVersion;
    private string? _releaseNotesMarkdown;
    private string? _errorMessage;
    private bool _forceCanUpdate;

    private UpdateService()
    {
        _mgr = new UpdateManager(new GithubSource(
            $"https://github.com/{GithubOwner}/{GithubRepo}",
            accessToken: null,
            prerelease: false));

        // Velopack's CurrentVersion is the installed-package version; it's null
        // on dev/local-publish builds. Fall back to the assembly's informational
        // version, which MSBuild derives from <Version> in the csproj — which is
        // read from PackageVersion.txt — so the two paths agree. Strip SemVer
        // build metadata (`+<commit-sha>`) appended by the SDK in git checkouts.
        var informational = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0];
        _currentVersion = _mgr.CurrentVersion?.ToString() ?? informational ?? "0.0.0";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateState State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        private set => SetField(ref _currentVersion, value);
    }

    public string? AvailableVersion
    {
        get => _availableVersion;
        private set => SetField(ref _availableVersion, value);
    }

    public string? ReleaseNotesMarkdown
    {
        get => _releaseNotesMarkdown;
        private set => SetField(ref _releaseNotesMarkdown, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    /// <summary>
    /// True when the running process can actually apply updates (installed via
    /// the Velopack installer). False on dev/local-publish builds — the check
    /// no-ops in that case and the UI shows an explanatory status.
    /// </summary>
    public bool CanUpdate => _forceCanUpdate || _mgr.IsInstalled;

    public static void CheckInBackground()
    {
        // Dev simulator: set LAZYBONES_FAKE_UPDATE=<version> to populate the
        // UI as if a real update were ready, without needing a Velopack install.
        // Inert when the env var is empty or unset.
        var fakeVersion = Environment.GetEnvironmentVariable("LAZYBONES_FAKE_UPDATE");
        if (!string.IsNullOrWhiteSpace(fakeVersion))
        {
            _ = Task.Run(() => Instance.SimulateUpdateReadyAsync(fakeVersion!.Trim()));
            return;
        }

        // Throttle background checks. Without this, every cycle-state change
        // would hit GitHub — fine in normal use (cycles are 60+ minutes apart)
        // but spammy if the user mashes Swap. Manual checks via the dashboard
        // bypass this gate by calling CheckAsync directly.
        var now = DateTime.UtcNow;
        if (now - Instance._lastBackgroundCheck < BackgroundCheckInterval) return;
        Instance._lastBackgroundCheck = now;

        _ = Task.Run(() => Instance.CheckAsync());
    }

    private async Task SimulateUpdateReadyAsync(string fakeVersion)
    {
        _forceCanUpdate = true;
        State = UpdateState.Checking;
        await Task.Delay(700);

        AvailableVersion = fakeVersion;
        ReleaseNotesMarkdown =
            $"v.{fakeVersion}\n" +
            "   - Simulated release populated by LAZYBONES_FAKE_UPDATE.\n" +
            "   - Restart-now will no-op in this mode; there's no real package downloaded.\n" +
            "   - Real release notes come from the GitHub Releases body.\n" +
            "\n" +
            "v.0.1.0\n" +
            "   - First public release of Get Up, Lazybones!\n" +
            "   - Sit/stand reminder timer with auto-pause on session lock.\n" +
            "   - Velopack auto-update from GitHub Releases.";
        State = UpdateState.UpdateReady;
    }

    public async Task CheckAsync()
    {
        if (!_mgr.IsInstalled) return;

        try
        {
            State = UpdateState.Checking;
            ErrorMessage = null;

            var update = await _mgr.CheckForUpdatesAsync();
            if (update is null)
            {
                _pendingUpdate = null;
                AvailableVersion = null;
                ReleaseNotesMarkdown = null;
                State = UpdateState.UpToDate;
                return;
            }

            _pendingUpdate = update;
            AvailableVersion = update.TargetFullRelease.Version?.ToString();

            await _mgr.DownloadUpdatesAsync(update);
            State = UpdateState.UpdateReady;

            _ = Task.Run(FetchReleaseNotesAsync);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = UpdateState.Failed;
        }
    }

    private async Task FetchReleaseNotesAsync()
    {
        var version = AvailableVersion;
        if (string.IsNullOrEmpty(version)) return;
        try
        {
            var url = $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/tags/v{version}";
            using var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
                ReleaseNotesMarkdown = body.GetString();
        }
        catch
        {
            // Best-effort — the tab falls back to "Release notes unavailable".
        }
    }

    /// <summary>
    /// Applies the downloaded update and restarts the app. Returns synchronously
    /// once Velopack has been told to take over; the process exits shortly after.
    /// </summary>
    public void ApplyAndRestart()
    {
        if (_pendingUpdate is null) return;
        _mgr.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        RaisePropertyChanged(propertyName);
    }

    private void RaisePropertyChanged(string? propertyName)
    {
        var handler = PropertyChanged;
        if (handler is null) return;
        var args = new PropertyChangedEventArgs(propertyName);
        if (Dispatcher.UIThread.CheckAccess())
            handler.Invoke(this, args);
        else
            Dispatcher.UIThread.Post(() => handler.Invoke(this, args));
    }
}
