using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class MicMonitorTests
{
    [Theory]
    [InlineData(123L, 0L, true)]     // started, not stopped -> in use
    [InlineData(123L, 456L, false)]  // started and stopped -> done
    [InlineData(null, 0L, false)]    // never started -> not in use
    [InlineData(123L, null, false)]  // missing stop -> conservative: not in use
    [InlineData(null, null, false)]
    public void IsInUse_TrueOnlyWhenStartedAndNotStopped(object? start, object? stop, bool expected)
    {
        Assert.Equal(expected, MicMonitor.IsInUse(start, stop));
    }
}
