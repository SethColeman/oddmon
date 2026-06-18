using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"oddmon-cfg-{Guid.NewGuid():N}.json");
        try
        {
            var c = new OddmonConfig { DiskSensitivity = 12.5, VolumePercent = 50, SoundEnabled = false };
            ConfigStore.Save(c, path);
            Assert.Equal(c, ConfigStore.Load(path)); // records compare by value
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveIfMissing_DoesNotClobberExistingFile()
    {
        // Regression: "Edit settings" re-open must not overwrite hand edits.
        string path = Path.Combine(Path.GetTempPath(), $"oddmon-cfg-{Guid.NewGuid():N}.json");
        try
        {
            var edited = new OddmonConfig { DiskSensitivity = 99.0 };
            ConfigStore.Save(edited, path);                  // simulate a hand edit on disk
            ConfigStore.SaveIfMissing(new OddmonConfig(), path); // re-open settings
            Assert.Equal(edited, ConfigStore.Load(path));    // edit survives
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_MissingOrCorrupt_ReturnsDefaults()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"oddmon-none-{Guid.NewGuid():N}.json");
        Assert.Equal(new OddmonConfig(), ConfigStore.Load(missing));
    }

    [Fact]
    public void VolumePercent_DefaultsTo15() =>
        Assert.Equal(15, new OddmonConfig().VolumePercent);

    [Fact]
    public void LegacyVolumeKey_IsIgnored()
    {
        string path = Path.Combine(Path.GetTempPath(), $"oddmon-legacy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"Volume\":0.3}"); // pre-percent format
            Assert.Equal(15, ConfigStore.Load(path).VolumePercent);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ToleratesCommentsAndTrailingCommas()
    {
        string path = Path.Combine(Path.GetTempPath(), $"oddmon-cmt-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "// a comment\n{\n  \"VolumePercent\": 42,\n}");
            Assert.Equal(42, ConfigStore.Load(path).VolumePercent);
        }
        finally { File.Delete(path); }
    }
}
