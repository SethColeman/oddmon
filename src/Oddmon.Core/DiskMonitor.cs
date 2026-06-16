using System.Diagnostics;

namespace Oddmon.Core;

/// <summary>
/// Polls Windows performance counters for whole-system disk throughput and
/// raises <see cref="LevelChanged"/> when the <see cref="ActivityLevel"/>
/// changes. Drives the HDD LED (M1) and, later, the seek sounds (M2). See
/// scope §3.2.
/// </summary>
public sealed class DiskMonitor : IDisposable
{
    private readonly PerformanceCounter _reads;
    private readonly PerformanceCounter _writes;
    private readonly System.Timers.Timer _timer;
    private readonly double _threshold;

    public ActivityLevel Current { get; private set; } = ActivityLevel.Idle;

    /// <summary>Raised on the timer thread whenever <see cref="Current"/> changes.</summary>
    public event Action<ActivityLevel>? LevelChanged;

    /// <param name="threshold">Ops/sec sensitivity floor (see <see cref="ActivityClassifier"/>).</param>
    /// <param name="pollIntervalMs">Poll period; ~66ms ≈ 15 Hz per scope §3.2.</param>
    public DiskMonitor(double threshold = 20.0, double pollIntervalMs = 66.0)
    {
        _threshold = threshold;

        // "_Total" sums all physical disks — whole-system activity is enough for v1.
        _reads = new PerformanceCounter("PhysicalDisk", "Disk Reads/sec", "_Total", readOnly: true);
        _writes = new PerformanceCounter("PhysicalDisk", "Disk Writes/sec", "_Total", readOnly: true);

        _timer = new System.Timers.Timer(pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Poll();
    }

    public void Start()
    {
        // First counter read always returns 0; prime it so the first real poll is meaningful.
        _reads.NextValue();
        _writes.NextValue();
        _timer.Start();
    }

    private void Poll()
    {
        // ponytail: no smoothing yet — threshold + change-detection only. Add an
        // EWMA or hold-timer if the LED flickers under bursty I/O (deferred from M1).
        var level = ActivityClassifier.Classify(_reads.NextValue(), _writes.NextValue(), _threshold);
        if (level == Current)
            return;

        Current = level;
        LevelChanged?.Invoke(level);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _reads.Dispose();
        _writes.Dispose();
    }
}
