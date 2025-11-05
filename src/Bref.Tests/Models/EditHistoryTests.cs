using Bref.Models;
using Xunit;

namespace Bref.Tests.Models;

public class EditHistoryTests
{
    [Fact]
    public void PushState_IncreasesUndoStack()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);

        // Act
        history.PushState(state1);
        var canUndoAfterFirst = history.CanUndo;
        history.PushState(state2);
        var canUndoAfterSecond = history.CanUndo;

        // Assert
        Assert.True(canUndoAfterFirst);
        Assert.True(canUndoAfterSecond);
    }

    [Fact]
    public void PushState_ClearsRedoStack()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);
        var state3 = CreateSegmentList(0, 30);

        // Act
        history.PushState(state1);
        history.PushState(state2);
        var undoneState = history.Undo(state2);
        var canRedoBeforePush = history.CanRedo;
        history.PushState(state3); // New action should clear redo stack
        var canRedoAfterPush = history.CanRedo;

        // Assert
        Assert.True(canRedoBeforePush);
        Assert.False(canRedoAfterPush);
    }

    [Fact]
    public void Undo_WithHistory_ReturnsPreviousState()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);
        var currentState = CreateSegmentList(0, 30);

        // Act
        history.PushState(state1);
        history.PushState(state2);
        var undoneState = history.Undo(currentState);

        // Assert
        Assert.NotNull(undoneState);
        Assert.Equal(TimeSpan.FromSeconds(20), undoneState.TotalDuration);
    }

    [Fact]
    public void Undo_EmptyHistory_ReturnsCurrentState()
    {
        // Arrange
        var history = new EditHistory();
        var currentState = CreateSegmentList(0, 30);

        // Act
        var undoneState = history.Undo(currentState);

        // Assert
        Assert.Same(currentState, undoneState);
    }

    [Fact]
    public void Redo_AfterUndo_ReturnsNextState()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);

        // Act
        history.PushState(state1);
        var undoneState = history.Undo(state2);
        var redoneState = history.Redo(undoneState);

        // Assert
        Assert.NotNull(redoneState);
        Assert.Equal(TimeSpan.FromSeconds(20), redoneState.TotalDuration);
    }

    [Fact]
    public void Redo_WithoutUndo_ReturnsCurrentState()
    {
        // Arrange
        var history = new EditHistory();
        var currentState = CreateSegmentList(0, 30);

        // Act
        var redoneState = history.Redo(currentState);

        // Assert
        Assert.Same(currentState, redoneState);
    }

    [Fact]
    public void UndoRedo_MultipleOperations_WorksCorrectly()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);
        var state3 = CreateSegmentList(0, 30);
        var state4 = CreateSegmentList(0, 40);

        // Act & Assert
        history.PushState(state1); // Stack: [state1]
        history.PushState(state2); // Stack: [state1, state2]
        history.PushState(state3); // Stack: [state1, state2, state3]

        var afterUndo1 = history.Undo(state4); // Returns state3, Stack: [state1, state2], Redo: [state4]
        Assert.Equal(TimeSpan.FromSeconds(30), afterUndo1.TotalDuration);

        var afterUndo2 = history.Undo(afterUndo1); // Returns state2, Stack: [state1], Redo: [state4, state3]
        Assert.Equal(TimeSpan.FromSeconds(20), afterUndo2.TotalDuration);

        var afterRedo1 = history.Redo(afterUndo2); // Returns state3, Stack: [state1, state2], Redo: [state4]
        Assert.Equal(TimeSpan.FromSeconds(30), afterRedo1.TotalDuration);

        var afterRedo2 = history.Redo(afterRedo1); // Returns state4, Stack: [state1, state2, state3], Redo: []
        Assert.Equal(TimeSpan.FromSeconds(40), afterRedo2.TotalDuration);

        Assert.False(history.CanRedo); // Redo stack should be empty
        Assert.True(history.CanUndo); // Undo stack should have items
    }

    [Fact]
    public void MaxHistoryDepth_LimitsStackSize()
    {
        // Arrange
        var history = new EditHistory { MaxHistoryDepth = 3 };
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);
        var state3 = CreateSegmentList(0, 30);
        var state4 = CreateSegmentList(0, 40);
        var state5 = CreateSegmentList(0, 50);

        // Act
        history.PushState(state1); // Stack: [state1]
        history.PushState(state2); // Stack: [state1, state2]
        history.PushState(state3); // Stack: [state1, state2, state3]
        history.PushState(state4); // Stack: [state2, state3, state4] (state1 removed)
        history.PushState(state5); // Stack: [state3, state4, state5] (state2 removed)

        // Verify we can undo 3 times but not 4
        var afterUndo1 = history.Undo(CreateSegmentList(0, 60)); // Returns state5
        Assert.Equal(TimeSpan.FromSeconds(50), afterUndo1.TotalDuration);

        var afterUndo2 = history.Undo(afterUndo1); // Returns state4
        Assert.Equal(TimeSpan.FromSeconds(40), afterUndo2.TotalDuration);

        var afterUndo3 = history.Undo(afterUndo2); // Returns state3
        Assert.Equal(TimeSpan.FromSeconds(30), afterUndo3.TotalDuration);

        var afterUndo4 = history.Undo(afterUndo3); // Should return state3 (no more history)
        Assert.Same(afterUndo3, afterUndo4); // Should return current state unchanged
    }

    [Fact]
    public void CanUndo_ReflectsStackState()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);

        // Assert - Empty history
        Assert.False(history.CanUndo);

        // Act & Assert - After push
        history.PushState(state1);
        Assert.True(history.CanUndo);

        // Act & Assert - After undo
        var undoneState = history.Undo(state2);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void CanRedo_ReflectsStackState()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);

        // Assert - Empty history
        Assert.False(history.CanRedo);

        // Act & Assert - After push (still no redo)
        history.PushState(state1);
        Assert.False(history.CanRedo);

        // Act & Assert - After undo (now can redo)
        var undoneState = history.Undo(state2);
        Assert.True(history.CanRedo);

        // Act & Assert - After redo (redo stack empty again)
        var redoneState = history.Redo(undoneState);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        // Arrange
        var history = new EditHistory();
        var state1 = CreateSegmentList(0, 10);
        var state2 = CreateSegmentList(0, 20);
        var state3 = CreateSegmentList(0, 30);

        // Act
        history.PushState(state1);
        history.PushState(state2);
        var undoneState = history.Undo(state3);

        // Verify we have both undo and redo history
        Assert.True(history.CanUndo);
        Assert.True(history.CanRedo);

        // Clear history
        history.Clear();

        // Assert
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    // Helper method to create a simple segment list for testing
    private SegmentList CreateSegmentList(double startSeconds, double endSeconds)
    {
        var segmentList = new SegmentList();
        segmentList.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.FromSeconds(startSeconds),
            SourceEnd = TimeSpan.FromSeconds(endSeconds)
        });
        return segmentList;
    }
}
