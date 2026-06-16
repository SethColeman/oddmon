using System.Diagnostics;

namespace Oddmon.Core;

/// <summary>
/// Polls Windows performance counters for whole-system disk activity and
/// raises <see cref="LevelChanged"/> when the <see cref="ActivityLevel"/>
/// changes. Drives the HDD LED (M1) and, later, the seek sounds (M2). See
/// scope §3.2.
/// </summary>
public sealed class DiskMonitor : IDisposable
{
    private readonly PerformanceCounter _idle;
    private readonly PerformanceCounter _readBytes;
    private readonly PerformanceCounter _writeBytes;
    private readonly System.Timers.Timer _timer;
    private readonly double _minBusyPercent;

    public ActivityLevel Current { get; private set; } = ActivityLevel.Idle;

    /// <summary>Raised on the timer thread whenever <see cref="Current"/> changes.</summary>
    public event Action<ActivityLevel>? LevelChanged;

    /// <param name="minBusyPercent">
    /// Disk-busy percentage (100 − idle) required to light the LED; lower is more
    /// sensitive. Default 8 lights on everyday activity while staying clear of the
    /// measured idle noise floor (~2–3% busy).
    /// </param>
    /// <param name="pollIntervalMs">Poll period; 100ms ≈ 10 Hz (scope §3.2: 10–20 Hz).</param>
    public DiskMonitor(double minBusyPercent = 8.0, double pollIntervalMs = 100.0)
    {
        _minBusyPercent = minBusyPercent;

        // "_Total" aggregates all physical disks — whole-system activity is enough for v1.
        _idle = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total", readOnly: true);
        _readBytes = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", readOnly: true);
        _writeBytes = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", readOnly: true);

        _timer = new System.Timers.Timer(pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Poll();
    }

    public void Start()
    {
        // First counter read always returns 0; prime them so the first poll is meaningful.
        _idle.NextValue();
        _readBytes.NextValue();
        _writeBytes.NextValue();
        _timer.Start();
    }

    private void Poll()
    {
        // ponytail: no smoothing yet — % Idle Time gate + change-detection only. Add a
        // short "stay-lit" hold if the LED still flickers under bursty I/O (deferred from M1).
        var level = ActivityClassifier.Classify(
            _idle.NextValue(), _readBytes.NextValue(), _writeBytes.NextValue(), _minBusyPercent);
        if (level == Current)
            return;

        Current = level;
        LevelChanged?.Invoke(level);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _idle.Dispose();
        _readBytes.Dispose();
        _writeBytes.Dispose();
    }
}
