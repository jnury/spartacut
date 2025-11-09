using System.Collections.Generic;

namespace SpartaCut.Core.Models;

/// <summary>
/// Manages undo/redo stack for segment operations
/// </summary>
public class EditHistory
{
    private Stack<SegmentList> _undoStack = new();
    private Stack<SegmentList> _redoStack = new();

    /// <summary>
    /// Maximum undo history depth (prevent memory issues)
    /// </summary>
    public int MaxHistoryDepth { get; set; } = 50;

    /// <summary>
    /// Can undo?
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Can redo?
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Push current state onto undo stack before making changes
    /// </summary>
    public void PushState(SegmentList currentState)
    {
        // Push clone to undo stack
        _undoStack.Push(currentState.Clone());

        // Clear redo stack (new action invalidates redo history)
        _redoStack.Clear();

        // Enforce max history depth - remove oldest entry if exceeded
        if (_undoStack.Count > MaxHistoryDepth)
        {
            // Convert to list to remove from bottom of stack
            var stackList = new List<SegmentList>(_undoStack);
            stackList.RemoveAt(stackList.Count - 1); // Remove oldest (bottom)

            // Rebuild stack
            _undoStack.Clear();
            for (int i = stackList.Count - 1; i >= 0; i--)
            {
                _undoStack.Push(stackList[i]);
            }
        }
    }

    /// <summary>
    /// Undo last operation
    /// </summary>
    public SegmentList Undo(SegmentList currentState)
    {
        if (!CanUndo)
        {
            // No history to undo, return current state unchanged
            return currentState;
        }

        // Push current state to redo stack
        _redoStack.Push(currentState.Clone());

        // Pop and return previous state from undo stack
        return _undoStack.Pop();
    }

    /// <summary>
    /// Redo last undone operation
    /// </summary>
    public SegmentList Redo(SegmentList currentState)
    {
        if (!CanRedo)
        {
            // No redo history, return current state unchanged
            return currentState;
        }

        // Push current state to undo stack
        _undoStack.Push(currentState.Clone());

        // Pop and return next state from redo stack
        return _redoStack.Pop();
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
