using Bref.Services;
using Xunit;
using System.Diagnostics;

namespace Bref.Tests.Performance;

public class FrameCachePerformanceTests
{
    [Fact]
    public void FrameCache_CachedAccess_MeetsPerformanceTarget()
    {
        // Skip if no test video
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4");

        if (!File.Exists(testVideoPath))
            return;

        // Arrange
        using var cache = new FrameCache(testVideoPath, capacity: 60);

        // Prime cache
        var _ = cache.GetFrame(TimeSpan.FromSeconds(5));

        // Act - Measure cached access time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var frame = cache.GetFrame(TimeSpan.FromSeconds(5));
        }
        sw.Stop();

        var avgTimeMs = sw.ElapsedMilliseconds / 100.0;

        // Assert - Target: <5ms per cached frame (200+ fps)
        Assert.True(avgTimeMs < 5, $"Cached frame access took {avgTimeMs:F2}ms (target: <5ms)");
    }

    [Fact]
    public void FrameCache_UncachedAccess_MeetsPerformanceTarget()
    {
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4");

        if (!File.Exists(testVideoPath))
            return;

        // Arrange
        using var cache = new FrameCache(testVideoPath, capacity: 60);

        // Act - Measure uncached decode time
        var times = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            cache.Clear(); // Ensure cache miss
            var sw = Stopwatch.StartNew();
            var frame = cache.GetFrame(TimeSpan.FromSeconds(i));
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }

        var avgTimeMs = times.Average();

        // Assert - Target: <100ms per uncached frame
        Assert.True(avgTimeMs < 100, $"Uncached frame decode took {avgTimeMs:F2}ms (target: <100ms)");
    }
}
