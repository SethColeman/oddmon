using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class DiskMonitorIntegrationTests
{
    // Integration: touches real performance counters and the disk. Timing-
    // dependent, so CI excludes "Category=Integration"; run it manually with:
    //   dotnet test --filter Category=Integration
    [Fact]
    [Trait("Category", "Integration")]
    public void Monitor_ReportsWriteActivity_WhenDiskIsBusy()
    {
        var seen = new HashSet<ActivityLevel>();
        using var monitor = new DiskMonitor(minBusyPercent: 10.0, pollIntervalMs: 50.0);
        monitor.LevelChanged += level => { lock (seen) seen.Add(level); }; // raised off-thread
        monitor.Start();

        // Generate sustained write activity: ~200 MB to a temp file, flushed to disk.
        string path = Path.Combine(Path.GetTempPath(), $"oddmon-disktest-{Guid.NewGuid():N}.tmp");
        try
        {
            var buffer = new byte[4 * 1024 * 1024];
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                                           bufferSize: 4096, FileOptions.WriteThrough))
            {
                for (int i = 0; i < 50; i++)
                    fs.Write(buffer);
                fs.Flush(flushToDisk: true);
            }
            Thread.Sleep(300); // let the counter and timer catch the tail of the activity
        }
        finally
        {
            File.Delete(path);
        }

        ActivityLevel[] observed;
        lock (seen) observed = seen.ToArray();

        Assert.Contains(observed, l => l is ActivityLevel.Write or ActivityLevel.Mixed);
    }
}
