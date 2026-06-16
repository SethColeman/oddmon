using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Oddmon.Core;

/// <summary>
/// Loads a sound set — every *.wav in a folder — as mono float clips at the
/// target sample rate. Empty result means "no set", and the caller falls back
/// to synthesized clicks. See scope §3.4.
/// </summary>
public static class SoundSet
{
    public static float[][] Load(string? dir, int sampleRate)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return [];

        var clips = new List<float[]>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.wav"))
        {
            try { clips.Add(ReadMonoSamples(path, sampleRate)); }
            catch { /* skip unreadable/corrupt wav */ }
        }
        return clips.ToArray();
    }

    private static float[] ReadMonoSamples(string path, int sampleRate)
    {
        using var reader = new AudioFileReader(path);
        ISampleProvider sp = reader;
        if (sp.WaveFormat.Channels > 1)
            sp = sp.ToMono();
        if (sp.WaveFormat.SampleRate != sampleRate)
            sp = new WdlResamplingSampleProvider(sp, sampleRate);

        var all = new List<float>();
        var buf = new float[sampleRate];
        int read;
        while ((read = sp.Read(buf, 0, buf.Length)) > 0)
            all.AddRange(buf.Take(read));
        return all.ToArray();
    }
}
