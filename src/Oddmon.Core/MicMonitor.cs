using Microsoft.Win32;

namespace Oddmon.Core;

/// <summary>
/// Detects whether any app is currently using the microphone — i.e. you're
/// probably in a call — by reading the same ConsentStore registry that drives
/// the Windows taskbar mic indicator. An app actively using the mic has
/// <c>LastUsedTimeStop == 0</c>. App-agnostic; covers Teams, Zoom, Meet, etc.
/// See scope §3.6.
/// </summary>
public sealed class MicMonitor : IDisposable
{
    private const string Root =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    private readonly System.Timers.Timer _timer;

    public bool InCall { get; private set; }
    public event Action<bool>? InCallChanged;

    public MicMonitor(double pollIntervalMs = 2000.0)
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
        bool inUse = AnyMicInUse();
        if (inUse == InCall)
            return;
        InCall = inUse;
        InCallChanged?.Invoke(inUse);
    }

    private static bool AnyMicInUse()
    {
        using var root = Registry.CurrentUser.OpenSubKey(Root);
        if (root is null)
            return false;

        foreach (var name in root.GetSubKeyNames())
        {
            using var sub = root.OpenSubKey(name);
            if (sub is null)
                continue;

            // Win32 apps live one level deeper under "NonPackaged"; packaged
            // (Store) apps store their values directly on the per-package key.
            if (name == "NonPackaged")
            {
                foreach (var app in sub.GetSubKeyNames())
                {
                    using var k = sub.OpenSubKey(app);
                    if (k is not null && IsInUse(k.GetValue("LastUsedTimeStart"), k.GetValue("LastUsedTimeStop")))
                        return true;
                }
            }
            else if (IsInUse(sub.GetValue("LastUsedTimeStart"), sub.GetValue("LastUsedTimeStop")))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>In use when it has started but not stopped (Stop == 0). Conservative: missing values = not in use.</summary>
    public static bool IsInUse(object? start, object? stop)
        => start is long && stop is long s && s == 0;

    public void Dispose() => _timer.Dispose();
}
