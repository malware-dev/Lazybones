using System;
using System.Runtime.Versioning;
using Avalonia.Threading;
using Microsoft.Win32;

namespace Lazybones.Features.SessionPresence;

[SupportedOSPlatform("windows")]
public sealed class WindowsUserPresenceMonitor : IUserPresenceMonitor
{
    public event EventHandler? Locked;
    public event EventHandler? Unlocked;

    public WindowsUserPresenceMonitor()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        // SystemEvents fires on a system thread; marshal to UI thread so
        // listeners can touch view-model state safely.
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            Dispatcher.UIThread.Post(() => Locked?.Invoke(this, EventArgs.Empty));
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Dispatcher.UIThread.Post(() => Unlocked?.Invoke(this, EventArgs.Empty));
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
