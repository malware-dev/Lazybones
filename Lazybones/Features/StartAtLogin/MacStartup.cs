using System;
using System.IO;
using System.Runtime.Versioning;

namespace Lazybones.Features.StartAtLogin;

[SupportedOSPlatform("macos")]
internal sealed class MacStartupService : IStartupService
{
    private const string Label = "com.malforge.lazybones";
    private const string LegacyLabel = "com.malforge.standup";

    private static readonly string LaunchAgentsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents");

    private static readonly string PlistPath = Path.Combine(LaunchAgentsDir, $"{Label}.plist");
    private static readonly string LegacyPlistPath = Path.Combine(LaunchAgentsDir, $"{LegacyLabel}.plist");

    public string LoginItemLabel => "Open at login";

    public bool IsEnabled => File.Exists(PlistPath) || File.Exists(LegacyPlistPath);

    public bool SetEnabled(bool enabled)
    {
        try
        {
            // Always remove the legacy "com.malforge.standup" plist — its
            // identifier doesn't match the bundle id of the current build.
            if (File.Exists(LegacyPlistPath))
                File.Delete(LegacyPlistPath);

            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return false;

                Directory.CreateDirectory(LaunchAgentsDir);
                File.WriteAllText(PlistPath, BuildPlist(path));
            }
            else if (File.Exists(PlistPath))
            {
                File.Delete(PlistPath);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string BuildPlist(string executablePath) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>{Label}</string>
            <key>ProgramArguments</key>
            <array>
                <string>{System.Security.SecurityElement.Escape(executablePath)}</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
        </dict>
        </plist>
        """;
}
