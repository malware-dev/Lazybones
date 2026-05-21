using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace Lazybones.Services;

internal static class UpdateService
{
    private const string GithubRepoUrl = "https://github.com/malware-dev/Lazybones";

    // Fire-and-forget: check GitHub Releases, download a newer version if found,
    // and queue it to apply on next launch. No-ops on dev/local-publish builds
    // (UpdateManager.IsInstalled is false unless the app was installed via Velopack).
    public static void CheckInBackground()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(GithubRepoUrl, accessToken: null, prerelease: false));
                if (!mgr.IsInstalled) return;

                var update = await mgr.CheckForUpdatesAsync();
                if (update is null) return;

                await mgr.DownloadUpdatesAsync(update);
                mgr.WaitExitThenApplyUpdates(update);
            }
            catch
            {
                // Best-effort — updates must never disrupt app startup.
            }
        });
    }
}
