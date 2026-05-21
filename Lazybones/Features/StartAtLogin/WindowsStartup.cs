#if WINDOWS
using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Lazybones.Features.StartAtLogin;

[SupportedOSPlatform("windows")]
internal sealed class WindowsStartupService : IStartupService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Lazybones";
    private const string LegacyAppName = "StandUp";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            // Always remove the legacy "StandUp" Run entry — its EXE path no
            // longer exists after the rename.
            key.DeleteValue(LegacyAppName, throwOnMissingValue: false);

            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return;
                key.SetValue(AppName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best-effort: registry write can fail under restricted environments.
        }
    }
}
#endif
