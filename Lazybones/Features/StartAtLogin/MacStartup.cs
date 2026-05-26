using System;
using System.IO;
using System.Runtime.Versioning;

namespace Lazybones.Features.StartAtLogin;

[SupportedOSPlatform("macos")]
internal sealed class MacStartupService : IStartupService
{
    private const string Label = "dev.malforge.lazybones";

    // Bundle identifiers we've shipped (or considered shipping) previously.
    // SetEnabled removes any plist using these labels on every call so users
    // upgrading from any prior identifier end up registered under the current
    // Label only. IsEnabled treats either current or any legacy plist as
    // "enabled" so the toggle correctly reflects existing-install state.
    private static readonly string[] LegacyLabels =
    {
        "com.malforge.standup",  // pre-rename (project's previous name)
        "com.malforge.lazybones" // pre-prefix-change (com → dev)
    };

    private static readonly string LaunchAgentsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents");

    private static readonly string PlistPath = Path.Combine(LaunchAgentsDir, $"{Label}.plist");

    private static string LegacyPlistPath(string label) =>
        Path.Combine(LaunchAgentsDir, $"{label}.plist");

    public string LoginItemLabel => "Open at login";

    public bool IsEnabled
    {
        get
        {
            if (File.Exists(PlistPath)) return true;
            foreach (var label in LegacyLabels)
                if (File.Exists(LegacyPlistPath(label))) return true;
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            foreach (var label in LegacyLabels)
            {
                var legacy = LegacyPlistPath(label);
                if (File.Exists(legacy)) File.Delete(legacy);
            }

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
