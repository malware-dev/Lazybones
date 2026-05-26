using System;
using System.Globalization;

namespace Lazybones.Features.Shell;

public static class TimeInputParser
{
    /// <summary>
    /// Parses a free-form time string. Accepts HH:MM:SS, MM:SS, plain numbers (minutes),
    /// suffixed values (5h / 30m / 90s), and relative deltas (+5m, -10s) anchored to <paramref name="relativeTo"/>.
    /// </summary>
    public static bool TryParse(string input, TimeSpan relativeTo, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim().ToLowerInvariant();

        bool isAddition = input.StartsWith('+');
        bool isSubtraction = input.StartsWith('-');

        if (isAddition || isSubtraction)
        {
            if (!TryParse(input[1..].Trim(), relativeTo, out var delta))
                return false;

            var combined = isAddition ? relativeTo.Add(delta) : relativeTo.Subtract(delta);
            result = combined < TimeSpan.Zero ? TimeSpan.Zero : combined;
            return true;
        }

        // HH:MM:SS or MM:SS
        if (input.Contains(':'))
        {
            var parts = input.Split(':');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var h) &&
                int.TryParse(parts[1], out var m) &&
                int.TryParse(parts[2], out var s) &&
                h >= 0 && m is >= 0 and <= 59 && s is >= 0 and <= 59)
            {
                result = new TimeSpan(h, m, s);
                return true;
            }
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var mm) &&
                int.TryParse(parts[1], out var ss) &&
                mm >= 0 && ss is >= 0 and <= 59)
            {
                result = new TimeSpan(0, mm, ss);
                return true;
            }
            return false;
        }

        // Suffixed formats: longest suffix wins to avoid ambiguity
        // (e.g. "5min" must match "min", not just "m").
        if (TryStripSuffix(input, out var numStr, "hours", "hour", "hrs", "hr", "h"))
            return TryFromUnit(numStr, 3600, out result);

        if (TryStripSuffix(input, out numStr, "minutes", "minute", "mins", "min", "m"))
            return TryFromUnit(numStr, 60, out result);

        if (TryStripSuffix(input, out numStr, "seconds", "second", "secs", "sec", "s"))
            return TryFromUnit(numStr, 1, out result);

        // Plain number (including decimals) - assume minutes
        return TryFromUnit(input.Replace(',', '.'), 60, out result);
    }

    private static bool TryFromUnit(string numStr, int unitInSeconds, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n < 0)
            return false;
        result = TimeSpan.FromSeconds((long)(n * unitInSeconds));
        return true;
    }

    private static bool TryStripSuffix(string input, out string remainder, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (input.EndsWith(suffix, StringComparison.Ordinal))
            {
                remainder = input[..^suffix.Length].Trim();
                return true;
            }
        }
        remainder = input;
        return false;
    }
}
