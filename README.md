# Get Up, Lazybones!

A small, opinionated sit/stand reminder for Windows and macOS.

A round, transparent desktop disk that counts down the current sit or stand
interval and prompts you when it's time to swap. Built on Avalonia, runs from
the system tray-adjacent corner of your screen.

## Features

- **Layered progress rings** around the disk — outer = today's standing
  minutes vs your daily goal, inner = the current cycle.
- **Dashboard window** with stats, achievements, and settings (Stats tab
  shows today's progress, current streak, and a 13-week heatmap).
- **Sixteen achievements** covering streaks, daily intensity, response
  engagement, and lifetime volume.
- **Session-lock-aware auto-pause** — locking the screen pauses the cycle,
  unlocking resumes it with a brief "Welcome back" toast. Any pause time is
  invisible to every metric: it doesn't count toward standing minutes, doesn't
  shift day buckets, doesn't disqualify engagement-based achievements.
- **Dim-by-default chrome** — buttons and window controls fade to dark gray
  when the cursor isn't over the disk.

## Status

Personal project, in active development. Currently runs cleanly on Windows.
macOS support is implemented (Cocoa runtime bindings for `NSDistributedNotificationCenter`)
but pending real-world verification on Mac hardware.

## Building

Requires the .NET 10 SDK.

```sh
dotnet run --project Lazybones --framework net10.0
```

On Windows, `net10.0-windows10.0.17763.0` is also available and is the target
the published build uses.

## License

[MIT](LICENSE)
