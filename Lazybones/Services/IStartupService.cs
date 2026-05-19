using System;

namespace Lazybones.Services;

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
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
    public void SetEnabled(bool enabled) { }
}
