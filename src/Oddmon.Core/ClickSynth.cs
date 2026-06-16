namespace Oddmon.Core;

/// <summary>
/// Synthesizes a short HDD-seek "click" — a noise burst with fast exponential
/// decay. Mono float samples in [-1, 1].
/// </summary>
// ponytail: synthesized placeholder, no asset/licensing needed. Swap for CC0 WAV
// sound sets when real samples are added (scope §3.4).
public static class ClickSynth
{
    public static float[] Generate(int sampleRate = 44100, int durationMs = 45, int seed = 1)
    {
        int n = sampleRate * durationMs / 1000;
        var buf = new float[n];
        var rng = new Random(seed);
        for (int i = 0; i < n; i++)
        {
            double decay = Math.Exp(-12.0 * i / n);
            double noise = rng.NextDouble() * 2 - 1;
            buf[i] = (float)(noise * decay * 0.6);
        }
        return buf;
    }
}
