# Get Up, Lazybones!

A small, opinionated sit/stand reminder for Windows and macOS.

A round, transparent desktop disk that counts down the current sit or stand
interval and prompts you when it's time to swap. Built on Avalonia.

## Features

- **Layered progress rings** around the disk — outer ring tracks today's
  standing minutes against your daily goal, inner ring tracks the current
  cycle.
- **Dashboard window** with stats, achievements, and settings.
- **Achievements** covering streaks, daily intensity, response engagement,
  and lifetime volume.
- **Session-lock-aware auto-pause** — locking the screen pauses the cycle,
  unlocking resumes it. Pause time is invisible to every metric: it doesn't
  count toward standing minutes, doesn't shift day buckets, doesn't
  disqualify engagement-based achievements.
- **Start at login** on both platforms — Windows uses the `Run` registry
  key, macOS uses a `LaunchAgent`.
- **Auto-update** via [Velopack](https://velopack.io) against GitHub
  Releases. Checks happen at startup; new versions download in the
  background and apply on next launch.
- **Dim-by-default chrome** — buttons and window controls fade when the
  cursor isn't over the disk.

## Installing

Grab the latest installer from the
[Releases page](https://github.com/malforge/Lazybones/releases/latest).

### Windows

1. Download the file ending in `win-Setup.exe`.
2. Run it. Windows SmartScreen will warn that the app is from an unknown
   publisher — that's expected, because Lazybones isn't code-signed (it's a
   free, open-source personal project, and code-signing certificates cost
   real money). Click **More info**, then **Run anyway**.
3. The installer runs and Lazybones starts.

### macOS

1. Download the installer for your Mac:
   - **Apple Silicon** (M1, M2, M3, M4): the file with `osx-arm64` in the name.
   - **Intel**: the file with `osx-x64` in the name.

   Not sure which you have? Click the Apple menu → **About This Mac** and
   look at the **Chip** or **Processor** line.

2. Open the downloaded `.pkg`. macOS will block it because the app isn't
   signed with an Apple Developer ID — that's expected, for the same reason
   as Windows above. In the warning, click **Done** (or **Cancel**) — **not
   "Move to Trash"**, which deletes the installer you just downloaded.
3. Open **System Settings → Privacy & Security** and scroll down to the
   **Security** section. You'll see a note that Lazybones was blocked, with an
   **Open Anyway** button — click it, authenticate, then confirm **Open** in
   the dialog. Follow the installer prompts.

Once installed, future updates download and apply themselves the next time
you launch the app — you don't need to repeat any of this.

## License

[MIT](LICENSE)
