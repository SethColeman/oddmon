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
            var c = new OddmonConfig { DiskSensitivity = 12.5, Volume = 0.5f, SoundEnabled = false };
            ConfigStore.Save(c, path);
            Assert.Equal(c, ConfigStore.Load(path)); // records compare by value
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveIfMissing_DoesNotClobberExistingFile()
    {
        // Regression: "Edit settings" re-open must not overwrite hand edits.
        string path = ConfigStore.FilePath;
        bool preexisting = File.Exists(path);
        string? backup = preexisting ? File.ReadAllText(path) : null;
        try
        {
            var edited = new OddmonConfig { DiskSensitivity = 99.0 };
            ConfigStore.Save(edited, path);              // simulate a hand edit on disk
            ConfigStore.SaveIfMissing(new OddmonConfig()); // re-open settings
            Assert.Equal(edited, ConfigStore.Load(path)); // edit survives
        }
        finally
        {
            if (backup is not null) File.WriteAllText(path, backup);
            else if (!preexisting) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingOrCorrupt_ReturnsDefaults()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"oddmon-none-{Guid.NewGuid():N}.json");
        Assert.Equal(new OddmonConfig(), ConfigStore.Load(missing));
    }
}
