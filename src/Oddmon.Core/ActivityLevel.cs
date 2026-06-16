namespace Oddmon.Core;

/// <summary>
/// Disk activity state that drives the HDD LED color and the seek-sound
/// intensity. See scope §3.2. This is the shared vocabulary the DiskMonitor
/// emits and the LedController / AudioEngine consume.
/// </summary>
public enum ActivityLevel
{
    Idle,
    Read,
    Write,
    Mixed,
}
