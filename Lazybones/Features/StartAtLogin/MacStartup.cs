using System;
using System.IO;
using System.Runtime.Versioning;

namespace Lazybones.Features.StartAtLogin;

[SupportedOSPlatform("macos")]
internal sealed class MacStartupService : IStartupService
{
    private const string Label = "com.malforge.standup";

    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    public bool IsEnabled => File.Exists(PlistPath);

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return;

                Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
                File.WriteAllText(PlistPath, BuildPlist(path));
            }
            else if (File.Exists(PlistPath))
            {
                File.Delete(PlistPath);
            }
        }
        catch
        {
            // Best-effort: writing to ~/Library/LaunchAgents can fail under sandboxing.
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
