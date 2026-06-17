using Microsoft.Win32;

namespace Oddmon.App;

/// <summary>
/// Opt-in launch-at-login via the per-user HKCU Run key (scope §4). Benign,
/// user-level autostart — visible and removable in Task Manager → Startup, and
/// scoped to the current user only (no machine-wide persistence, no elevation).
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "oddmon";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
