using System;

namespace Lazybones.Features.StartAtLogin;

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

public static class StartupService
{
    public static IStartupService Instance { get; } = Create();

    public static string LoginItemLabel =>
        OperatingSystem.IsMacOS() ? "Open at login" : "Start with Windows";

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
    public void SetEnabled(bool enabled) { }
}
