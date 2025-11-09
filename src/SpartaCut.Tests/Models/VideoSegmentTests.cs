using SpartaCut.Core.Models;
using Xunit;

namespace SpartaCut.Tests.Models;

public class VideoSegmentTests
{
    [Fact]
    public void Duration_CalculatesCorrectly()
    {
        // Arrange
        var segment = new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(10),
            SourceEnd = TimeSpan.FromSeconds(30)
        };

        // Act
        var duration = segment.Duration;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(20), duration);
    }

    [Fact]
    public void Contains_ReturnsTrueForTimeInRange()
    {
        // Arrange
        var segment = new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(10),
            SourceEnd = TimeSpan.FromSeconds(30)
        };

        // Act
        var result = segment.Contains(TimeSpan.FromSeconds(20));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ReturnsFalseForTimeOutsideRange()
    {
        // Arrange
        var segment = new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(10),
            SourceEnd = TimeSpan.FromSeconds(30)
        };

        // Act
        var resultBefore = segment.Contains(TimeSpan.FromSeconds(5));
        var resultAfter = segment.Contains(TimeSpan.FromSeconds(35));

        // Assert
        Assert.False(resultBefore);
        Assert.False(resultAfter);
    }

    [Fact]
    public void Contains_ReturnsTrueForBoundaryTimes()
    {
        // Arrange
        var segment = new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(10),
            SourceEnd = TimeSpan.FromSeconds(30)
        };

        // Act
        var resultAtStart = segment.Contains(TimeSpan.FromSeconds(10));
        var resultAtEnd = segment.Contains(TimeSpan.FromSeconds(30));

        // Assert
        Assert.True(resultAtStart);
        Assert.True(resultAtEnd);
    }
}
