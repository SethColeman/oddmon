using System.Text.Json;

namespace Oddmon.Core;

/// <summary>User settings, persisted as JSON. Hand-editable (scope §4).</summary>
public sealed record OddmonConfig
{
    /// <summary>Disk-busy % to light the LED / click; lower is more sensitive (scope §3.2).</summary>
    public double DiskSensitivity { get; init; } = 8.0;
    public float Volume { get; init; } = 0.3f;
    public bool SoundEnabled { get; init; } = true;

    /// <summary>Folder of WAV clips; null uses the bundled set, else synth (scope §3.4).</summary>
    public string? SoundSetPath { get; init; }

    public bool OverlayEnabled { get; init; }
    public int? OverlayX { get; init; }
    public int? OverlayY { get; init; }

    /// <summary>Launch oddmon at login (HKCU Run entry, opt-in). See scope §4.</summary>
    public bool Autostart { get; init; }

    /// <summary>Quiet-hours window as "HH:mm" strings; null/unparseable disables it (scope §3.6).</summary>
    public string? QuietHoursStart { get; init; }
    public string? QuietHoursEnd { get; init; }

    /// <summary>True if <paramref name="now"/> falls in the quiet-hours window. Pure, wrap-around aware.</summary>
    public bool InQuietHours(DateTime now)
    {
        if (!TimeOnly.TryParse(QuietHoursStart, out var start) || !TimeOnly.TryParse(QuietHoursEnd, out var end))
            return false;
        if (start == end)
            return false; // empty window
        var t = TimeOnly.FromDateTime(now);
        return start < end ? t >= start && t < end   // same-day window, e.g. 09:00–17:00
                           : t >= start || t < end;   // wraps midnight, e.g. 22:00–07:00
    }
}

/// <summary>Loads/saves <see cref="OddmonConfig"/> at %APPDATA%\Oddmon\config.json.</summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oddmon");
    public static string FilePath => Path.Combine(Dir, "config.json");

    public static OddmonConfig Load() => Load(FilePath);

    public static OddmonConfig Load(string path)
    {
        try { return JsonSerializer.Deserialize<OddmonConfig>(File.ReadAllText(path)) ?? new(); }
        catch { return new(); } // missing or corrupt -> defaults
    }

    public static void Save(OddmonConfig config)
    {
        Directory.CreateDirectory(Dir);
        Save(config, FilePath);
    }

    public static void Save(OddmonConfig config, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(config, Opts));
}
