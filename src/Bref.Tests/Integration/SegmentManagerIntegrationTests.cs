using System;
using System.Linq;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Integration;

/// <summary>
/// Integration tests for SegmentManager.
/// Tests complex scenarios with multiple operations and state changes.
/// </summary>
public class SegmentManagerIntegrationTests
{
    [Fact]
    public void Scenario_SingleDeletion_UndoRedo()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Act - Delete [10s - 20s]
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        // Assert - Virtual duration should be 50s
        Assert.Equal(TimeSpan.FromSeconds(50), manager.CurrentSegments.TotalDuration);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);

        // Act - Undo
        manager.Undo();

        // Assert - Back to 60s
        Assert.Equal(TimeSpan.FromSeconds(60), manager.CurrentSegments.TotalDuration);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);

        // Act - Redo
        manager.Redo();

        // Assert - Back to 50s
        Assert.Equal(TimeSpan.FromSeconds(50), manager.CurrentSegments.TotalDuration);
        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
    }

    [Fact]
    public void Scenario_MultipleDeletions_ComplexUndo()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(120));

        // Act - Multiple deletions
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)); // Now 110s
        Assert.Equal(TimeSpan.FromSeconds(110), manager.CurrentSegments.TotalDuration);

        manager.DeleteSegment(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40)); // Now 100s
        Assert.Equal(TimeSpan.FromSeconds(100), manager.CurrentSegments.TotalDuration);

        manager.DeleteSegment(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(60)); // Now 90s
        Assert.Equal(TimeSpan.FromSeconds(90), manager.CurrentSegments.TotalDuration);

        // Act - Undo twice
        manager.Undo(); // Back to 100s
        manager.Undo(); // Back to 110s

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(110), manager.CurrentSegments.TotalDuration);
        Assert.True(manager.CanUndo); // Can still undo the first deletion
        Assert.True(manager.CanRedo); // Can redo the two undone deletions

        // Act - Redo once
        manager.Redo(); // Forward to 100s

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(100), manager.CurrentSegments.TotalDuration);
        Assert.True(manager.CanUndo);
        Assert.True(manager.CanRedo); // Can still redo one more
    }

    [Fact]
    public void Scenario_DeletionInMiddle_SplitsSegment()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(60));

        // Act - Delete [20s - 40s]
        manager.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40));

        // Assert - Should have 2 segments
        Assert.Equal(2, manager.CurrentSegments.SegmentCount);

        // Verify first segment: [0s - 20s]
        var segment1 = manager.CurrentSegments.KeptSegments[0];
        Assert.Equal(TimeSpan.FromSeconds(0), segment1.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(20), segment1.SourceEnd);

        // Verify second segment: [40s - 60s]
        var segment2 = manager.CurrentSegments.KeptSegments[1];
        Assert.Equal(TimeSpan.FromSeconds(40), segment2.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(60), segment2.SourceEnd);

        // Verify virtual 15s maps to source 15s (in first segment)
        var source15 = manager.CurrentSegments.VirtualToSourceTime(TimeSpan.FromSeconds(15));
        Assert.Equal(TimeSpan.FromSeconds(15), source15);

        // Verify virtual 25s maps to source 45s (in second segment)
        // Virtual 20s = end of first segment
        // Virtual 25s = 5s into second segment = source 40s + 5s = 45s
        var source25 = manager.CurrentSegments.VirtualToSourceTime(TimeSpan.FromSeconds(25));
        Assert.Equal(TimeSpan.FromSeconds(45), source25);
    }

    [Fact]
    public void Scenario_SequentialDeletions_UpdatesVirtualTime()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(90));

        // Act - First deletion [10s - 20s] (now 80s)
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(80), manager.CurrentSegments.TotalDuration);

        // Act - Second deletion [20s - 30s] in virtual timeline
        // This should map to source [30s - 40s] because the first 10s deletion shifts everything
        manager.DeleteSegment(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(70), manager.CurrentSegments.TotalDuration);

        // Assert - Verify we have 3 segments
        Assert.Equal(3, manager.CurrentSegments.SegmentCount);

        // Segment 1: [0s - 10s]
        var seg1 = manager.CurrentSegments.KeptSegments[0];
        Assert.Equal(TimeSpan.FromSeconds(0), seg1.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(10), seg1.SourceEnd);

        // Segment 2: [20s - 30s] (the gap from first deletion is [10s - 20s])
        var seg2 = manager.CurrentSegments.KeptSegments[1];
        Assert.Equal(TimeSpan.FromSeconds(20), seg2.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(30), seg2.SourceEnd);

        // Segment 3: [40s - 90s] (the gap from second deletion is [30s - 40s])
        var seg3 = manager.CurrentSegments.KeptSegments[2];
        Assert.Equal(TimeSpan.FromSeconds(40), seg3.SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(90), seg3.SourceEnd);

        // Verify virtual 25s maps to source 45s
        // Virtual 0-10s = source 0-10s (seg1)
        // Virtual 10-20s = source 20-30s (seg2)
        // Virtual 20-70s = source 40-90s (seg3)
        // Virtual 25s = 5s into seg3 = source 40s + 5s = 45s
        var source25 = manager.CurrentSegments.VirtualToSourceTime(TimeSpan.FromSeconds(25));
        Assert.Equal(TimeSpan.FromSeconds(45), source25);
    }

    [Fact]
    public void Scenario_NewActionAfterUndo_ClearsRedoStack()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(100));

        // Act - Make two deletions
        manager.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        manager.DeleteSegment(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40));

        // Act - Undo one
        manager.Undo();
        Assert.True(manager.CanRedo);

        // Act - Make new deletion (should clear redo stack)
        manager.DeleteSegment(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(60));

        // Assert - Redo should no longer be available
        Assert.False(manager.CanRedo);
        Assert.True(manager.CanUndo); // But undo should still work
    }

    [Fact]
    public void Scenario_MaxHistoryDepth_RemovesOldest()
    {
        // Arrange
        var manager = new SegmentManager();
        manager.Initialize(TimeSpan.FromSeconds(100));

        // Set max history depth to 5
        manager.History.MaxHistoryDepth = 5;

        // Act - Make 10 deletions (each 5 seconds)
        for (int i = 0; i < 10; i++)
        {
            var start = i * 5;
            var end = start + 5;
            manager.DeleteSegment(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
        }

        // Final duration should be 50s (removed 10 * 5s = 50s)
        Assert.Equal(TimeSpan.FromSeconds(50), manager.CurrentSegments.TotalDuration);

        // Act - Try to undo 6 times (should only succeed 5 times due to max depth)
        int undoCount = 0;
        for (int i = 0; i < 6; i++)
        {
            if (manager.CanUndo)
            {
                manager.Undo();
                undoCount++;
            }
            else
            {
                break;
            }
        }

        // Assert - Only 5 undos should have worked
        Assert.Equal(5, undoCount);
        Assert.False(manager.CanUndo); // No more undo available

        // Duration should be 75s (undid 5 deletions = restored 25s)
        Assert.Equal(TimeSpan.FromSeconds(75), manager.CurrentSegments.TotalDuration);
    }
}
