using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class AudioOutputsTests
{
    private static readonly string[] Devices =
        { "Speakers (Realtek Audio)", "Jabra SPEAK 510 USB", "Headphones" };

    [Fact]
    public void Match_Substring_CaseInsensitive() =>
        Assert.Equal("Speakers (Realtek Audio)", AudioOutputs.Match(Devices, "speakers"));

    [Fact]
    public void Match_ExactName() =>
        Assert.Equal("Jabra SPEAK 510 USB", AudioOutputs.Match(Devices, "Jabra SPEAK 510 USB"));

    [Fact]
    public void Match_NoMatch_ReturnsNull() =>
        Assert.Null(AudioOutputs.Match(Devices, "Bluetooth"));

    [Fact]
    public void Match_NullOrBlank_ReturnsNull()
    {
        Assert.Null(AudioOutputs.Match(Devices, null));
        Assert.Null(AudioOutputs.Match(Devices, "   "));
    }

    [Fact]
    public void Match_FirstOfMultiple() =>
        Assert.Equal("Speakers (Realtek Audio)", AudioOutputs.Match(Devices, "a")); // first containing 'a'

    [Fact]
    public void Names_NeverThrows() =>
        Assert.NotNull(AudioOutputs.Names()); // empty or populated, but never throws
}
