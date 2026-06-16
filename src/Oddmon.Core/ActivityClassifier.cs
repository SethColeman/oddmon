namespace Oddmon.Core;

/// <summary>
/// Maps a raw disk-throughput sample to an <see cref="ActivityLevel"/>.
/// Pure and stateless so the decision logic is unit-testable without real
/// performance counters. See scope §3.2.
/// </summary>
public static class ActivityClassifier
{
    /// <param name="readsPerSec">Disk Reads/sec from the perf counter.</param>
    /// <param name="writesPerSec">Disk Writes/sec from the perf counter.</param>
    /// <param name="threshold">
    /// Ops/sec below which a direction counts as idle. This is the "sensitivity"
    /// knob — higher means the LED ignores more micro-activity.
    /// </param>
    public static ActivityLevel Classify(double readsPerSec, double writesPerSec, double threshold)
    {
        bool reading = readsPerSec >= threshold;
        bool writing = writesPerSec >= threshold;

        return (reading, writing) switch
        {
            (true, true) => ActivityLevel.Mixed,
            (true, false) => ActivityLevel.Read,
            (false, true) => ActivityLevel.Write,
            (false, false) => ActivityLevel.Idle,
        };
    }
}
