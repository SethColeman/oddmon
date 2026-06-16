using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class ActivityClassifierTests
{
    private const double Threshold = 20.0;

    [Theory]
    [InlineData(0, 0, ActivityLevel.Idle)]
    [InlineData(5, 5, ActivityLevel.Idle)]       // below threshold both ways
    [InlineData(100, 0, ActivityLevel.Read)]
    [InlineData(0, 100, ActivityLevel.Write)]
    [InlineData(100, 100, ActivityLevel.Mixed)]
    [InlineData(20, 0, ActivityLevel.Read)]      // exactly at threshold counts as active
    [InlineData(100, 5, ActivityLevel.Read)]     // write below threshold is ignored
    public void Classify_MapsThroughputToLevel(double reads, double writes, ActivityLevel expected)
    {
        Assert.Equal(expected, ActivityClassifier.Classify(reads, writes, Threshold));
    }
}
