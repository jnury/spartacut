using System;
using Bref.Models;

namespace Bref.Tests.Models;

public class SegmentListTests
{
    #region Basic Operations Tests

    [Fact]
    public void TotalDuration_EmptyList_ReturnsZero()
    {
        // Arrange
        var segmentList = new SegmentList();

        // Act
        var duration = segmentList.TotalDuration;

        // Assert
        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void TotalDuration_SingleSegment_ReturnsSegmentDuration()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });

        // Act
        var duration = segmentList.TotalDuration;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), duration);
    }

    [Fact]
    public void TotalDuration_MultipleSegments_ReturnsSumOfDurations()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(40),
            SourceEnd = TimeSpan.FromSeconds(45)
        });

        // Act
        var duration = segmentList.TotalDuration;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(25), duration); // 10 + 10 + 5
    }

    [Fact]
    public void VirtualToSourceTime_WithSingleSegment_MapsCorrectly()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(5),
            SourceEnd = TimeSpan.FromSeconds(15)
        });

        // Act & Assert
        Assert.Equal(TimeSpan.FromSeconds(5), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(0)));
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(15), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void VirtualToSourceTime_WithMultipleSegments_MapsCorrectly()
    {
        // Arrange
        var segmentList = new SegmentList();
        // Segment 1: 0-10s (virtual 0-10s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        // Segment 2: 20-30s (virtual 10-20s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act & Assert
        // First segment
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(0)));
        Assert.Equal(TimeSpan.FromSeconds(5), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(9.9), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(9.9)));

        // Second segment
        Assert.Equal(TimeSpan.FromSeconds(20), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(25), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(15)));
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(20)));
    }

    [Fact]
    public void VirtualToSourceTime_BeyondEnd_ReturnsLastSegmentEnd()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });

        // Act
        var result = segmentList.VirtualToSourceTime(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), result);
    }

    [Fact]
    public void SourceToVirtualTime_InKeptSegment_ReturnsVirtualTime()
    {
        // Arrange
        var segmentList = new SegmentList();
        // Segment 1: 0-10s (virtual 0-10s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        // Segment 2: 20-30s (virtual 10-20s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act & Assert
        // First segment
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(0)));
        Assert.Equal(TimeSpan.FromSeconds(5), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(10)));

        // Second segment
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(20)));
        Assert.Equal(TimeSpan.FromSeconds(15), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(25)));
        Assert.Equal(TimeSpan.FromSeconds(20), segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void SourceToVirtualTime_InDeletedRegion_ReturnsNull()
    {
        // Arrange
        var segmentList = new SegmentList();
        // Segment 1: 0-10s (virtual 0-10s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        // Segment 2: 20-30s (virtual 10-20s)
        // Gap: 10-20s is deleted
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act & Assert - times in deleted region
        Assert.Null(segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(15)));
        Assert.Null(segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(10.5)));
        Assert.Null(segmentList.SourceToVirtualTime(TimeSpan.FromSeconds(19.9)));
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        // Arrange
        var original = new SegmentList();
        original.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        original.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act
        var clone = original.Clone();

        // Assert - verify deep copy
        Assert.NotSame(original, clone);
        Assert.NotSame(original.KeptSegments, clone.KeptSegments);
        Assert.Equal(original.KeptSegments.Count, clone.KeptSegments.Count);

        // Verify segment data copied
        Assert.Equal(original.KeptSegments[0].SourceStart, clone.KeptSegments[0].SourceStart);
        Assert.Equal(original.KeptSegments[0].SourceEnd, clone.KeptSegments[0].SourceEnd);
        Assert.Equal(original.KeptSegments[1].SourceStart, clone.KeptSegments[1].SourceStart);
        Assert.Equal(original.KeptSegments[1].SourceEnd, clone.KeptSegments[1].SourceEnd);

        // Verify segments are different instances
        Assert.NotSame(original.KeptSegments[0], clone.KeptSegments[0]);
        Assert.NotSame(original.KeptSegments[1], clone.KeptSegments[1]);

        // Verify modifying clone doesn't affect original
        clone.KeptSegments[0].SourceStart = TimeSpan.FromSeconds(100);
        Assert.NotEqual(original.KeptSegments[0].SourceStart, clone.KeptSegments[0].SourceStart);
    }

    #endregion

    #region Deletion Logic Tests

    [Fact]
    public void DeleteSegment_EntireSegment_RemovesSegment()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete entire first segment (virtual 0-10s)
        segmentList.DeleteSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));

        // Assert
        Assert.Single(segmentList.KeptSegments);
        Assert.Equal(TimeSpan.FromSeconds(20), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.KeptSegments[0].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_MiddleOfSegment_SplitsIntoTwo()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete middle portion (virtual 10-20s → source 10-20s)
        segmentList.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        // Assert
        Assert.Equal(2, segmentList.KeptSegments.Count);

        // First segment: 0-10s
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.KeptSegments[0].SourceEnd);

        // Second segment: 20-30s
        Assert.Equal(TimeSpan.FromSeconds(20), segmentList.KeptSegments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.KeptSegments[1].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_StartOfSegment_TrimsStart()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete first 10 seconds
        segmentList.DeleteSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));

        // Assert
        Assert.Single(segmentList.KeptSegments);
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.KeptSegments[0].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_EndOfSegment_TrimsEnd()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete last 10 seconds (virtual 20-30s → source 20-30s)
        segmentList.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));

        // Assert
        Assert.Single(segmentList.KeptSegments);
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(20), segmentList.KeptSegments[0].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_SpanningMultipleSegments_RemovesAll()
    {
        // Arrange
        var segmentList = new SegmentList();
        // Segment 1: source 0-10s (virtual 0-10s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        // Segment 2: source 20-30s (virtual 10-20s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });
        // Segment 3: source 40-50s (virtual 20-30s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(40),
            SourceEnd = TimeSpan.FromSeconds(50)
        });

        // Act - Delete virtual 5-25s (spans all three segments partially)
        segmentList.DeleteSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(25));

        // Assert
        Assert.Equal(2, segmentList.KeptSegments.Count);

        // First segment: source 0-5s (trimmed end)
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(5), segmentList.KeptSegments[0].SourceEnd);

        // Second segment: source 45-50s (trimmed start)
        Assert.Equal(TimeSpan.FromSeconds(45), segmentList.KeptSegments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(50), segmentList.KeptSegments[1].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_PartialOverlapStart_TrimsCorrectly()
    {
        // Arrange
        var segmentList = new SegmentList();
        // Segment 1: source 0-10s (virtual 0-10s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });
        // Segment 2: source 20-30s (virtual 10-20s)
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(20),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete virtual 5-15s (overlaps end of first segment and start of second)
        segmentList.DeleteSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(2, segmentList.KeptSegments.Count);

        // First segment: source 0-5s (trimmed end)
        Assert.Equal(TimeSpan.FromSeconds(0), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(5), segmentList.KeptSegments[0].SourceEnd);

        // Second segment: source 25-30s (trimmed start)
        Assert.Equal(TimeSpan.FromSeconds(25), segmentList.KeptSegments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.KeptSegments[1].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_PartialOverlapEnd_TrimsCorrectly()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(10),
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Act - Delete virtual 5-15s
        // Virtual 5s maps to source 15s (10 + 5)
        // So we're deleting source 15-25s
        segmentList.DeleteSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal(2, segmentList.KeptSegments.Count);

        // First segment: source 10-15s
        Assert.Equal(TimeSpan.FromSeconds(10), segmentList.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(15), segmentList.KeptSegments[0].SourceEnd);

        // Second segment: source 25-30s
        Assert.Equal(TimeSpan.FromSeconds(25), segmentList.KeptSegments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), segmentList.KeptSegments[1].SourceEnd);
    }

    [Fact]
    public void DeleteSegment_InvalidRange_ThrowsException()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });

        // Act & Assert - virtualEnd <= virtualStart
        Assert.Throws<ArgumentException>(() =>
            segmentList.DeleteSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

        Assert.Throws<ArgumentException>(() =>
            segmentList.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void DeleteSegment_BeyondDuration_ThrowsException()
    {
        // Arrange
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(0),
            SourceEnd = TimeSpan.FromSeconds(10)
        });

        // Act & Assert - virtualStart beyond total duration
        Assert.Throws<ArgumentException>(() =>
            segmentList.DeleteSegment(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(20)));
    }

    #endregion
}
