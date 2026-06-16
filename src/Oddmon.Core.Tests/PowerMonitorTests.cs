using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class PowerMonitorTests
{
    [Theory]
    // On AC: mode drives brightness
    [InlineData(true, false, PowerMode.BestPerformance, TurboState.Bright)]
    [InlineData(true, false, PowerMode.Balanced, TurboState.Dim)]
    [InlineData(true, false, PowerMode.BestEfficiency, TurboState.Off)]
    // On battery: only flat-out perf is (dim) turbo
    [InlineData(false, false, PowerMode.BestPerformance, TurboState.Dim)]
    [InlineData(false, false, PowerMode.Balanced, TurboState.Off)]
    [InlineData(false, false, PowerMode.BestEfficiency, TurboState.Off)]
    // Battery saver always wins -> off
    [InlineData(true, true, PowerMode.BestPerformance, TurboState.Off)]
    public void Classify_MapsPowerStateToTurbo(bool onAc, bool saver, PowerMode mode, TurboState expected)
    {
        Assert.Equal(expected, PowerMonitor.Classify(onAc, saver, mode));
    }
}
