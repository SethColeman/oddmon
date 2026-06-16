using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class ClickSynthTests
{
    [Fact]
    public void Generate_IsRightLength_InRange_AndDecays()
    {
        var buf = ClickSynth.Generate(sampleRate: 44100, durationMs: 45);

        Assert.Equal(44100 * 45 / 1000, buf.Length);
        Assert.All(buf, s => Assert.InRange(s, -1f, 1f));

        // Tail must be quieter than the head — proves the decay envelope works.
        float head = Peak(buf, 0, buf.Length / 10);
        float tail = Peak(buf, buf.Length * 9 / 10, buf.Length);
        Assert.True(tail < head, $"expected decay: tail {tail} < head {head}");
    }

    private static float Peak(float[] buf, int start, int end)
    {
        float max = 0;
        for (int i = start; i < end; i++)
            max = Math.Max(max, Math.Abs(buf[i]));
        return max;
    }
}
