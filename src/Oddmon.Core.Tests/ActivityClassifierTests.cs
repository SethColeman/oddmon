using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class ActivityClassifierTests
{
    private const double MinBusy = 20.0; // light when disk is >20% busy
    private const double MB = 1024 * 1024;

    [Theory]
    // idle gate: disk barely busy -> off, regardless of byte volume
    [InlineData(98.0, 50 * MB, 50 * MB, ActivityLevel.Idle)]  // 2% busy, below gate
    [InlineData(85.0, 50 * MB, 0, ActivityLevel.Idle)]        // 15% busy, still below gate
    // busy gate tripped: color follows read/write byte volume
    [InlineData(50.0, 50 * MB, 1 * MB, ActivityLevel.Read)]   // reads dominate
    [InlineData(50.0, 1 * MB, 50 * MB, ActivityLevel.Write)]  // writes dominate
    [InlineData(50.0, 50 * MB, 50 * MB, ActivityLevel.Mixed)] // balanced
    [InlineData(50.0, 0, 0, ActivityLevel.Mixed)]             // busy, no measurable transfer
    [InlineData(80.0, 50 * MB, 0, ActivityLevel.Read)]        // exactly at 20% busy gate
    public void Classify_GatesOnIdleTime_ThenColorsByBytes(
        double percentIdle, double readBytes, double writeBytes, ActivityLevel expected)
    {
        Assert.Equal(expected, ActivityClassifier.Classify(percentIdle, readBytes, writeBytes, MinBusy));
    }
}
