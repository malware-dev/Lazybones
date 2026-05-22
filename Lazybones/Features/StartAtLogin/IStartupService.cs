using System;

namespace Lazybones.Features.StartAtLogin;

public interface IStartupService
{
    bool IsEnabled { get; }
    string LoginItemLabel { get; }

    /// <summary>
    /// Attempts to enable or disable launch-at-login. Returns true on success, false if the
    /// underlying OS write failed (e.g. registry access denied, sandboxed filesystem).
    /// Callers should re-read IsEnabled before persisting their own state.
    /// </summary>
    bool SetEnabled(bool enabled);
}

public static class StartupService
{
    public static IStartupService Instance { get; } = Create();

    private static IStartupService Create()
    {
        if (OperatingSystem.IsWindows())
        {
#if WINDOWS
            return new WindowsStartupService();
#endif
        }

        if (OperatingSystem.IsMacOS())
            return new MacStartupService();

        return new NoOpStartupService();
    }
}

internal sealed class NoOpStartupService : IStartupService
{
    public bool IsEnabled => false;
    public bool SetEnabled(bool enabled) => false;
    public string LoginItemLabel => "Start at login";
}
