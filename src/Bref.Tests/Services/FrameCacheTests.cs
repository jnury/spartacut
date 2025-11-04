using Bref.Services;
using Bref.Models;
using Xunit;

namespace Bref.Tests.Services;

public class FrameCacheTests
{
    [Fact]
    public void GetFrame_WithColdCache_DecodesAndCaches()
    {
        // This test requires a real video file
        // Skip if not available
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4");

        if (!File.Exists(testVideoPath))
            return; // Skip test

        // Arrange
        using var cache = new FrameCache(testVideoPath, capacity: 10);

        // Act
        var frame = cache.GetFrame(TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(frame);
        Assert.True(frame.Width > 0);
        Assert.True(frame.Height > 0);
    }

    [Fact]
    public void GetFrame_WithHotCache_ReturnsQuickly()
    {
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4");

        if (!File.Exists(testVideoPath))
            return; // Skip test

        // Arrange
        using var cache = new FrameCache(testVideoPath, capacity: 10);

        // Prime cache
        var frame1 = cache.GetFrame(TimeSpan.FromSeconds(1));

        // Act - Second access should be from cache
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var frame2 = cache.GetFrame(TimeSpan.FromSeconds(1));
        sw.Stop();

        // Assert
        Assert.NotNull(frame2);
        Assert.True(sw.ElapsedMilliseconds < 10, "Cached frame should return in <10ms");
    }
}
