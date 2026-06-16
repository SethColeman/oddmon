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
}

/// <summary>Loads/saves <see cref="OddmonConfig"/> at %APPDATA%\Oddmon\config.json.</summary>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Oddmon");
    private static string FilePath => Path.Combine(Dir, "config.json");

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
