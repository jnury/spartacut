using Bref.Core.Models;
using Xunit;

namespace Bref.Tests.Models;

public class TimelineMetricsTests
{
    [Fact]
    public void PixelsPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var pixelsPerSecond = metrics.PixelsPerSecond;

        // Assert
        Assert.Equal(10, pixelsPerSecond); // 1000 pixels / 100 seconds = 10 px/s
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 100)]
    [InlineData(50, 500)]
    [InlineData(100, 1000)]
    public void TimeToPixel_ConvertsCorrectly(double seconds, double expectedPixel)
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var pixel = metrics.TimeToPixel(TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.Equal(expectedPixel, pixel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 10)]
    [InlineData(500, 50)]
    [InlineData(1000, 100)]
    public void PixelToTime_ConvertsCorrectly(double pixel, double expectedSeconds)
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var time = metrics.PixelToTime(pixel);

        // Assert
        Assert.Equal(expectedSeconds, time.TotalSeconds);
    }
}
