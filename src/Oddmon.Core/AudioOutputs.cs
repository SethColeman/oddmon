using NAudio.CoreAudioApi;

namespace Oddmon.Core;

/// <summary>Render-device discovery for output selection; names match Windows Sound settings.</summary>
public static class AudioOutputs
{
    /// <summary>Active render-device friendly names; empty if enumeration fails.</summary>
    public static IReadOnlyList<string> Names()
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                     .Select(d => d.FriendlyName).ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>First name containing <paramref name="saved"/> (case-insensitive), or null
    /// when <paramref name="saved"/> is null/blank or nothing matches. Pure.</summary>
    public static string? Match(IReadOnlyList<string> names, string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved)) return null;
        return names.FirstOrDefault(n => n.Contains(saved, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Active render <see cref="MMDevice"/> whose name matches, else null. Caller disposes.</summary>
    public static MMDevice? Resolve(string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved)) return null;
        try
        {
            // The returned MMDevice keeps its own COM ref, so disposing the enumerator is safe.
            using var en = new MMDeviceEnumerator();
            return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                     .FirstOrDefault(d => d.FriendlyName.Contains(saved, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }
}
