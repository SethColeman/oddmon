namespace Oddmon.Core;

/// <summary>
/// Maps a raw disk sample to an <see cref="ActivityLevel"/>. Pure and stateless
/// so the decision logic is unit-testable without real performance counters.
/// See scope §3.2.
/// </summary>
/// <remarks>
/// Profiling an idle Windows machine showed Disk Writes/sec sitting at a median
/// of ~44 (background metadata/logging), which made an ops/sec threshold light
/// the LED almost constantly. "% Idle Time" stays at ~97%+ when idle and only
/// drops under genuine load, so it's the on/off gate. Read-vs-write *bytes*
/// (transfer volume, not op count) then pick the color — a 4 KB log flush no
/// longer outweighs a real transfer.
/// </remarks>
public static class ActivityClassifier
{
    // How lopsided read/write volume must be to count as a single direction
    // rather than Mixed. 0.66 => one side must be ~2x the other.
    private const double DirectionShare = 0.66;

    /// <param name="percentIdleTime">"% Idle Time" counter (0–100).</param>
    /// <param name="readBytesPerSec">Disk Read Bytes/sec.</param>
    /// <param name="writeBytesPerSec">Disk Write Bytes/sec.</param>
    /// <param name="minBusyPercent">
    /// Minimum disk-busy percentage (100 − idle) for the LED to light. Lower is
    /// more sensitive. This is the "sensitivity" knob from scope §3.2.
    /// </param>
    public static ActivityLevel Classify(
        double percentIdleTime, double readBytesPerSec, double writeBytesPerSec, double minBusyPercent)
    {
        double busyPercent = 100.0 - percentIdleTime;
        if (busyPercent < minBusyPercent)
            return ActivityLevel.Idle;

        double total = readBytesPerSec + writeBytesPerSec;
        if (total <= 0)
            return ActivityLevel.Mixed; // busy but no measurable transfer — generic activity

        double readShare = readBytesPerSec / total;
        if (readShare >= DirectionShare) return ActivityLevel.Read;
        if (readShare <= 1.0 - DirectionShare) return ActivityLevel.Write;
        return ActivityLevel.Mixed;
    }
}
