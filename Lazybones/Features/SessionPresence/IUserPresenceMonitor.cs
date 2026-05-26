using System;

namespace Lazybones.Features.SessionPresence;

// Surfaces "user is away from the desk" via the OS's session-lock signal,
// which is dramatically less noisy than input-idle. Locked fires when the
// workstation locks (Win+L, screensaver-lock, screen-locks-when-display-sleeps,
// macOS screen lock). Unlocked fires on resume.
public interface IUserPresenceMonitor : IDisposable
{
    event EventHandler? Locked;
    event EventHandler? Unlocked;
}

public static class UserPresenceMonitor
{
    public static IUserPresenceMonitor Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsUserPresenceMonitor();
        if (OperatingSystem.IsMacOS()) return new MacUserPresenceMonitor();
        return new NoOpUserPresenceMonitor();
    }
}

public sealed class NoOpUserPresenceMonitor : IUserPresenceMonitor
{
    public event EventHandler? Locked { add { } remove { } }
    public event EventHandler? Unlocked { add { } remove { } }
    public void Dispose() { }
}
