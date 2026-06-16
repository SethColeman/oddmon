using System.Runtime.InteropServices;

namespace Oddmon.Core;

public enum PowerMode { BestEfficiency, Balanced, BestPerformance }
public enum TurboState { Off, Dim, Bright }

/// <summary>
/// Reflects whether the machine is running flat-out or throttled, mapped from
/// AC/battery status and the Windows power-mode slider — the nostalgic "Turbo"
/// light. See scope §3.3.
/// </summary>
public sealed class PowerMonitor : IDisposable
{
    // Windows power-overlay GUIDs. Balanced is reported as GUID_ZERO.
    private static readonly Guid OverlayBestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid OverlayBestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    private readonly System.Timers.Timer _timer;

    public TurboState Current { get; private set; } = TurboState.Off;
    public event Action<TurboState>? TurboChanged;

    public PowerMonitor(double pollIntervalMs = 3000.0)
    {
        _timer = new System.Timers.Timer(pollIntervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    private void Poll()
    {
        var state = Classify(IsOnAc(out bool saver), saver, ReadMode());
        if (state == Current)
            return;
        Current = state;
        TurboChanged?.Invoke(state);
    }

    /// <summary>Scope §3.3 default mapping (configurable later). Pure for testing.</summary>
    public static TurboState Classify(bool onAc, bool batterySaver, PowerMode mode)
    {
        if (batterySaver)
            return TurboState.Off;

        if (onAc)
            return mode switch
            {
                PowerMode.BestPerformance => TurboState.Bright,
                PowerMode.Balanced => TurboState.Dim,
                _ => TurboState.Off,
            };

        // On battery: only flat-out performance counts as (dim) turbo.
        return mode == PowerMode.BestPerformance ? TurboState.Dim : TurboState.Off;
    }

    private static bool IsOnAc(out bool batterySaver)
    {
        batterySaver = false;
        if (GetSystemPowerStatus(out var s))
        {
            batterySaver = (s.SystemStatusFlag & 1) == 1;
            return s.ACLineStatus == 1;
        }
        return true; // unknown (e.g. desktop) -> treat as plugged in
    }

    private static PowerMode ReadMode()
    {
        if (PowerGetEffectiveOverlayScheme(out Guid g) == 0)
        {
            if (g == OverlayBestPerformance) return PowerMode.BestPerformance;
            if (g == OverlayBestEfficiency) return PowerMode.BestEfficiency;
        }
        return PowerMode.Balanced; // GUID_ZERO or failure
    }

    public void Dispose() => _timer.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag; // bit 0 = battery saver (Win10+)
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid scheme);
}
