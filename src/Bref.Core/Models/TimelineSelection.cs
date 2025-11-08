using System;

namespace Bref.Core.Models;

/// <summary>
/// Represents a user's selection on the timeline.
/// </summary>
public class TimelineSelection
{
    /// <summary>
    /// Whether a selection is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Start time of selection (virtual timeline).
    /// </summary>
    public TimeSpan SelectionStart { get; private set; }

    /// <summary>
    /// End time of selection (virtual timeline).
    /// May be before or after SelectionStart depending on drag direction.
    /// </summary>
    public TimeSpan SelectionEnd { get; private set; }

    /// <summary>
    /// Duration of selection (always positive).
    /// </summary>
    public TimeSpan Duration =>
        TimeSpan.FromSeconds(Math.Abs((SelectionEnd - SelectionStart).TotalSeconds));

    /// <summary>
    /// Normalized start (always the earlier time).
    /// </summary>
    public TimeSpan NormalizedStart =>
        SelectionStart < SelectionEnd ? SelectionStart : SelectionEnd;

    /// <summary>
    /// Normalized end (always the later time).
    /// </summary>
    public TimeSpan NormalizedEnd =>
        SelectionStart < SelectionEnd ? SelectionEnd : SelectionStart;

    /// <summary>
    /// Whether the selection is valid (non-zero duration).
    /// </summary>
    public bool IsValid => IsActive && Duration.TotalSeconds > 0.01; // 10ms minimum

    /// <summary>
    /// Start a new selection at the given time.
    /// </summary>
    public void StartSelection(TimeSpan time)
    {
        IsActive = true;
        SelectionStart = time;
        SelectionEnd = time;
    }

    /// <summary>
    /// Update the selection end point.
    /// </summary>
    public void UpdateSelection(TimeSpan time)
    {
        SelectionEnd = time;
    }

    /// <summary>
    /// Clear the current selection.
    /// </summary>
    public void ClearSelection()
    {
        IsActive = false;
        SelectionStart = TimeSpan.Zero;
        SelectionEnd = TimeSpan.Zero;
    }
}
