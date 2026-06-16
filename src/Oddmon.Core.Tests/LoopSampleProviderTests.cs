using NAudio.Wave;
using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class LoopSampleProviderTests
{
    [Fact]
    public void Read_WrapsAroundAndFillsFully()
    {
        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        var loop = new LoopSampleProvider([[1f, 2f, 3f]], fmt);

        var buf = new float[7];
        int n = loop.Read(buf, 0, buf.Length);

        Assert.Equal(7, n); // always fills
        Assert.Equal(new[] { 1f, 2f, 3f, 1f, 2f, 3f, 1f }, buf);
    }
}
