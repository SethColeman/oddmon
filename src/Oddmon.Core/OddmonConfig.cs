using System.Text.Json;

namespace Oddmon.Core;

/// <summary>User settings, persisted as JSON. Hand-editable (scope §4).</summary>
public sealed record OddmonConfig
{
    /// <summary>Disk-busy % to light the LED / click; lower is more sensitive (scope §3.2).</summary>
    public double DiskSensitivity { get; init; } = 8.0;
    /// <summary>Master volume as a percent, 0–100; converted to 0–1 for the player (scope §1).</summary>
    public int VolumePercent { get; init; } = 15;
    public bool SoundEnabled { get; init; } = true;

    /// <summary>Folder of WAV clips; null uses the bundled set, else synth (scope §3.4).</summary>
    public string? SoundSetPath { get; init; }

    /// <summary>Output device friendly-name substring; null uses the Windows default (scope §4).</summary>
    public string? OutputDevice { get; init; }

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
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Written on every save so the docs survive a tray-menu rewrite (scope §4).
    private const string Header = """
// oddmon config — https://github.com/SethColeman/oddmon
// Edits apply on next launch. The tray menu rewrites this file; this header is
// preserved, but any comments you add elsewhere are not.
// VolumePercent      : 0-100        master volume, e.g. 15
// DiskSensitivity    : 0-100        disk-busy % to trigger LED/sound; lower = more sensitive, e.g. 8
// SoundEnabled       : true | false manual mute
// SoundSetPath       : null | "C:\\path\\to\\wavs"   null = bundled set
// OutputDevice       : null | "name substring"       null = Windows default, e.g. "Speakers"
// OverlayEnabled     : true | false ; OverlayX / OverlayY : int | null
// QuietHoursStart/End: "HH:mm" | null   wraps midnight, e.g. "22:00" / "07:00"
// Autostart          : true | false launch at login

""";

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oddmon");
    public static string FilePath => Path.Combine(Dir, "config.json");

    public static OddmonConfig Load() => Load(FilePath);

    public static OddmonConfig Load(string path)
    {
        try { return JsonSerializer.Deserialize<OddmonConfig>(File.ReadAllText(path), ReadOpts) ?? new(); }
        catch { return new(); } // missing or corrupt -> defaults
    }

    public static void Save(OddmonConfig config)
    {
        Directory.CreateDirectory(Dir);
        Save(config, FilePath);
    }

    /// <summary>Write defaults only if no file exists yet; never clobber hand edits.</summary>
    public static void SaveIfMissing(OddmonConfig config)
    {
        if (!File.Exists(FilePath)) Save(config); // Save() creates the dir
    }

    public static void SaveIfMissing(OddmonConfig config, string path)
    {
        if (!File.Exists(path)) Save(config, path);
    }

    public static void Save(OddmonConfig config, string path) =>
        File.WriteAllText(path, Header + JsonSerializer.Serialize(config, WriteOpts));
}
