using System;
using Lazybones.Features.Shell;
using Xunit;

namespace Lazybones.Tests.Features.Shell;

public class TimeInputParserTests
{
    private static readonly TimeSpan Anchor = TimeSpan.FromMinutes(30);

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Rejects_blank_or_null_input(string? input, bool _)
    {
        Assert.False(TimeInputParser.TryParse(input!, Anchor, out var result));
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Theory]
    [InlineData("1:30:00", 1, 30, 0)]
    [InlineData("00:00:00", 0, 0, 0)]
    [InlineData("12:34:56", 12, 34, 56)]
    public void HH_MM_SS_parses(string input, int h, int m, int s)
    {
        Assert.True(TimeInputParser.TryParse(input, Anchor, out var result));
        Assert.Equal(new TimeSpan(h, m, s), result);
    }

    [Theory]
    [InlineData("5:30", 0, 5, 30)]
    [InlineData("90:00", 0, 90, 0)]
    public void MM_SS_parses(string input, int h, int m, int s)
    {
        Assert.True(TimeInputParser.TryParse(input, Anchor, out var result));
        Assert.Equal(new TimeSpan(h, m, s), result);
    }

    [Theory]
    [InlineData("1:60:00")] // minutes overflow
    [InlineData("1:00:60")] // seconds overflow
    [InlineData("0:99")]    // seconds overflow in MM:SS
    [InlineData("a:b:c")]   // non-numeric
    [InlineData("1:2:3:4")] // too many parts
    public void Rejects_invalid_colon_forms(string input)
    {
        Assert.False(TimeInputParser.TryParse(input, Anchor, out _));
    }

    [Theory]
    [InlineData("5h", 5 * 3600)]
    [InlineData("5hrs", 5 * 3600)]
    [InlineData("2hours", 2 * 3600)]
    [InlineData("30m", 30 * 60)]
    [InlineData("45min", 45 * 60)]
    [InlineData("15minutes", 15 * 60)]
    [InlineData("90s", 90)]
    [InlineData("30secs", 30)]
    [InlineData("1second", 1)]
    public void Suffixed_units_parse(string input, int expectedSeconds)
    {
        Assert.True(TimeInputParser.TryParse(input, Anchor, out var result));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Fact]
    public void Longest_suffix_wins_over_shorter_overlap()
    {
        // "5min" must consume "min" (not just "m") so the remainder is "5", not "5in".
        Assert.True(TimeInputParser.TryParse("5min", Anchor, out var result));
        Assert.Equal(TimeSpan.FromMinutes(5), result);
    }

    [Theory]
    [InlineData("10", 10 * 60)]   // plain number = minutes
    [InlineData("0.5", 30)]        // 0.5 minutes = 30 seconds
    [InlineData("1,5", 90)]        // comma decimal accepted
    public void Plain_number_treated_as_minutes(string input, int expectedSeconds)
    {
        Assert.True(TimeInputParser.TryParse(input, Anchor, out var result));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Fact]
    public void Positive_delta_adds_to_anchor()
    {
        Assert.True(TimeInputParser.TryParse("+5m", Anchor, out var result));
        Assert.Equal(Anchor + TimeSpan.FromMinutes(5), result);
    }

    [Fact]
    public void Negative_delta_subtracts_from_anchor()
    {
        Assert.True(TimeInputParser.TryParse("-10s", Anchor, out var result));
        Assert.Equal(Anchor - TimeSpan.FromSeconds(10), result);
    }

    [Fact]
    public void Negative_delta_clamped_to_zero_on_underflow()
    {
        // Anchor is 30m; subtracting 1h underflows.
        Assert.True(TimeInputParser.TryParse("-1h", Anchor, out var result));
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Theory]
    [InlineData("-5")]    // bare negative no unit → -5 minutes from anchor
    [InlineData("+0.5h")] // delta with decimal
    public void Delta_with_various_unit_forms_succeeds(string input)
    {
        Assert.True(TimeInputParser.TryParse(input, Anchor, out _));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("5x")] // unknown suffix
    public void Rejects_garbage_input(string input)
    {
        Assert.False(TimeInputParser.TryParse(input, Anchor, out _));
    }

    [Fact]
    public void Bare_negative_treated_as_negative_delta_in_minutes()
    {
        // "-1" parses as a -1-minute delta from the anchor — documents actual permissive
        // parser behavior (any leading sign opens a delta clause).
        Assert.True(TimeInputParser.TryParse("-1", Anchor, out var result));
        Assert.Equal(Anchor - TimeSpan.FromMinutes(1), result);
    }

    [Fact]
    public void Double_sign_compounds_via_recursion()
    {
        // "++5m" → outer + recurses on "+5m" → inner + recurses on "5m" = 5m,
        // inner result = anchor+5m = 35m, outer result = anchor+35m = 65m.
        // Documents the recursive-delta behavior; not necessarily desirable but
        // pinning it so a future change is intentional.
        Assert.True(TimeInputParser.TryParse("++5m", Anchor, out var result));
        Assert.Equal(TimeSpan.FromMinutes(65), result);
    }

    [Fact]
    public void Whitespace_around_input_is_trimmed()
    {
        Assert.True(TimeInputParser.TryParse("  5m  ", Anchor, out var result));
        Assert.Equal(TimeSpan.FromMinutes(5), result);
    }

    [Fact]
    public void Case_insensitive_suffix()
    {
        Assert.True(TimeInputParser.TryParse("5MIN", Anchor, out var result));
        Assert.Equal(TimeSpan.FromMinutes(5), result);
    }
}
