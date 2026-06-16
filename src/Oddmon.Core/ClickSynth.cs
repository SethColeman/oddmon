namespace Oddmon.Core;

/// <summary>
/// Synthesizes a short HDD-seek "tick": a damped resonance (the actuator knock)
/// with a brief noise transient on attack. Mono float samples in [-1, 1].
/// </summary>
// ponytail: synthesized placeholder, no asset/licensing needed. Swap for CC0 WAV
// sound sets when real samples are added (scope §3.4).
public static class ClickSynth
{
    public static float[] Generate(int sampleRate = 44100, double freqHz = 1400, int durationMs = 30, int seed = 1)
    {
        int n = sampleRate * durationMs / 1000;
        var buf = new float[n];
        var rng = new Random(seed);
        double w = 2 * Math.PI * freqHz / sampleRate;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double body = Math.Sin(w * i) * Math.Exp(-22.0 * t);          // resonant knock
            double attack = (rng.NextDouble() * 2 - 1) * Math.Exp(-180.0 * t); // sharp transient
            buf[i] = (float)((body * 0.7 + attack * 0.5) * 0.5);
        }
        return buf;
    }
}
