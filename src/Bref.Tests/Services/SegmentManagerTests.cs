using System;
using Bref.Core.Models;
using Bref.Core.Services;

namespace Bref.Tests.Services;

public class SegmentManagerTests
{
    [Fact]
    public void Initialize_CreatesFullVideoSegment()
    {
        // Arrange
        var manager = new SegmentManager();
        var duration = TimeSpan.FromSeconds(60);

        // Act
        manager.Initialize(duration);

        // Assert
        Assert.NotNull(manager.CurrentSegments);
        Assert.Single(manager.CurrentSegments.KeptSegments);
        Assert.Equal(TimeSpan.Zero, manager.CurrentSegments.KeptSegments[0].SourceStart);
        Assert.Equal(duration, manager.CurrentSegments.KeptSegments[0].SourceEnd);
        Assert.Equal(duration, manager.CurrentSegments.TotalDuration);
        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void DeleteSegment_PushesStateToHistory()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Act
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        // Assert - should now be able to undo
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void DeleteSegment_UpdatesCurrentSegments()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Act - delete middle segment [10-20]
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        // Assert - should have 2 segments: [0-10] and [20-60]
        Assert.Equal(2, manager.CurrentSegments.SegmentCount);
        Assert.Equal(TimeSpan.FromSeconds(50), manager.CurrentSegments.TotalDuration);

        // Check first segment
        Assert.Equal(TimeSpan.Zero, manager.CurrentSegments.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(10), manager.CurrentSegments.KeptSegments[0].SourceEnd);

        // Check second segment
        Assert.Equal(TimeSpan.FromSeconds(20), manager.CurrentSegments.KeptSegments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(60), manager.CurrentSegments.KeptSegments[1].SourceEnd);
    }

    [Fact]
    public void Undo_RestoresPreviousState()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));
        var initialDuration = manager.CurrentSegments.TotalDuration;

        // Act - delete and then undo
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        manager.Undo();

        // Assert - should be back to initial state
        Assert.Single(manager.CurrentSegments.KeptSegments);
        Assert.Equal(initialDuration, manager.CurrentSegments.TotalDuration);
        Assert.Equal(TimeSpan.Zero, manager.CurrentSegments.KeptSegments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(60), manager.CurrentSegments.KeptSegments[0].SourceEnd);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
    }

    [Fact]
    public void Redo_RestoresNextState()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Act - delete, undo, then redo
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        manager.Undo();
        manager.Redo();

        // Assert - should be back to deleted state
        Assert.Equal(2, manager.CurrentSegments.SegmentCount);
        Assert.Equal(TimeSpan.FromSeconds(50), manager.CurrentSegments.TotalDuration);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void UndoRedo_WithMultipleDeletions_WorksCorrectly()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(100));

        // Act - Complex scenario: delete, delete, undo, delete, undo, redo

        // Delete [10-20], should have 90 seconds
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(90), manager.CurrentSegments.TotalDuration);

        // Delete [20-30] in virtual time (which is [30-40] in source), should have 80 seconds
        manager.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(80), manager.CurrentSegments.TotalDuration);

        // Undo - back to 90 seconds (only first deletion)
        manager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(90), manager.CurrentSegments.TotalDuration);

        // Delete [0-5] in virtual time, should have 85 seconds
        manager.DeleteSegment(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(85), manager.CurrentSegments.TotalDuration);

        // Undo - back to 90 seconds
        manager.Undo();
        Assert.Equal(TimeSpan.FromSeconds(90), manager.CurrentSegments.TotalDuration);

        // Redo - back to 85 seconds
        manager.Redo();
        Assert.Equal(TimeSpan.FromSeconds(85), manager.CurrentSegments.TotalDuration);

        // Final state verification
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void GetSegmentAtVirtualTime_ReturnsCorrectSegment()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Delete middle section [20-40], creating segments [0-20] and [40-60]
        manager.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40));

        // Act & Assert
        // Virtual time 10s should be in first segment [0-20]
        var segment1 = manager.GetSegmentAtVirtualTime(TimeSpan.FromSeconds(10));
        Assert.NotNull(segment1);
        Assert.Equal(TimeSpan.Zero, segment1.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(20), segment1.SourceEnd);

        // Virtual time 30s should be in second segment [40-60]
        var segment2 = manager.GetSegmentAtVirtualTime(TimeSpan.FromSeconds(30));
        Assert.NotNull(segment2);
        Assert.Equal(TimeSpan.FromSeconds(40), segment2.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(60), segment2.SourceEnd);

        // Virtual time 0 should be in first segment
        var segment3 = manager.GetSegmentAtVirtualTime(TimeSpan.Zero);
        Assert.NotNull(segment3);
        Assert.Equal(TimeSpan.Zero, segment3.SourceStart);

        // Virtual time at boundary (20s) should be in second segment
        var segment4 = manager.GetSegmentAtVirtualTime(TimeSpan.FromSeconds(20));
        Assert.NotNull(segment4);
        Assert.Equal(TimeSpan.FromSeconds(40), segment4.SourceStart);

        // Virtual time beyond end should return null
        var segmentBeyond = manager.GetSegmentAtVirtualTime(TimeSpan.FromSeconds(100));
        Assert.Null(segmentBeyond);
    }

    [Fact]
    public void GetSegmentAtSourceTime_ReturnsCorrectSegment()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Delete middle section [20-40], creating segments [0-20] and [40-60]
        manager.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40));

        // Act & Assert
        // Source time 10s is in first segment [0-20]
        var segment1 = manager.GetSegmentAtSourceTime(TimeSpan.FromSeconds(10));
        Assert.NotNull(segment1);
        Assert.Equal(TimeSpan.Zero, segment1.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(20), segment1.SourceEnd);

        // Source time 50s is in second segment [40-60]
        var segment2 = manager.GetSegmentAtSourceTime(TimeSpan.FromSeconds(50));
        Assert.NotNull(segment2);
        Assert.Equal(TimeSpan.FromSeconds(40), segment2.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(60), segment2.SourceEnd);

        // Source time 30s is in deleted region, should return null
        var segmentDeleted = manager.GetSegmentAtSourceTime(TimeSpan.FromSeconds(30));
        Assert.Null(segmentDeleted);

        // Source time beyond end should return null
        var segmentBeyond = manager.GetSegmentAtSourceTime(TimeSpan.FromSeconds(100));
        Assert.Null(segmentBeyond);
    }
}
