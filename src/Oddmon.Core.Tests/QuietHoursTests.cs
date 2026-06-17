using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class QuietHoursTests
{
    private static DateTime At(int h, int m = 0) => new(2026, 1, 1, h, m, 0);

    [Theory]
    // Wrap-around window 22:00–07:00 (the interesting case)
    [InlineData("22:00", "07:00", 23, true)]
    [InlineData("22:00", "07:00", 3, true)]
    [InlineData("22:00", "07:00", 7, false)]   // end is exclusive
    [InlineData("22:00", "07:00", 12, false)]
    [InlineData("22:00", "07:00", 22, true)]   // start is inclusive
    // Same-day window 09:00–17:00
    [InlineData("09:00", "17:00", 12, true)]
    [InlineData("09:00", "17:00", 8, false)]
    [InlineData("09:00", "17:00", 17, false)]
    public void InQuietHours_Windows(string start, string end, int hour, bool expected)
    {
        var cfg = new OddmonConfig { QuietHoursStart = start, QuietHoursEnd = end };
        Assert.Equal(expected, cfg.InQuietHours(At(hour)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("nonsense", "07:00")]
    [InlineData("22:00", "22:00")]   // empty window
    public void InQuietHours_DisabledOrInvalid_AlwaysFalse(string? start, string? end)
    {
        var cfg = new OddmonConfig { QuietHoursStart = start, QuietHoursEnd = end };
        Assert.False(cfg.InQuietHours(At(3)));
        Assert.False(cfg.InQuietHours(At(15)));
    }
}
