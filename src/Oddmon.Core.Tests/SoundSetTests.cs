using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class SoundSetTests
{
    // The fallback trigger: no usable set -> empty -> caller synthesizes.
    [Fact]
    public void Load_NullOrMissingDir_ReturnsEmpty()
    {
        Assert.Empty(SoundSet.Load(null, 44100));
        Assert.Empty(SoundSet.Load(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}"), 44100));
    }
}
