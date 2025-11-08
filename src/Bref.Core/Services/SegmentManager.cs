using System;
using System.Collections.Generic;
using System.Linq;
using Bref.Core.Models;

namespace Bref.Core.Services;

/// <summary>
/// Manages video segments and edit history
/// Heart of the virtual timeline system
/// </summary>
public class SegmentManager
{
    private SegmentList _currentSegments = null!;
    private EditHistory _history = null!;

    /// <summary>
    /// Current segment list (virtual timeline)
    /// </summary>
    public SegmentList CurrentSegments => _currentSegments;

    /// <summary>
    /// Edit history for undo/redo
    /// </summary>
    public EditHistory History => _history;

    /// <summary>
    /// Can undo?
    /// </summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>
    /// Can redo?
    /// </summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Initialize with full video as single segment
    /// </summary>
    public void Initialize(TimeSpan videoDuration)
    {
        _currentSegments = new SegmentList
        {
            KeptSegments = new List<VideoSegment>
            {
                new VideoSegment { SourceStart = TimeSpan.Zero, SourceEnd = videoDuration }
            }
        };
        _history = new EditHistory();
    }

    /// <summary>
    /// Delete a segment from virtual timeline
    /// </summary>
    public void DeleteSegment(TimeSpan virtualStart, TimeSpan virtualEnd)
    {
        // 1. Push current state to history FIRST
        _history.PushState(_currentSegments);

        // 2. Perform deletion
        _currentSegments.DeleteSegment(virtualStart, virtualEnd);
    }

    /// <summary>
    /// Undo last operation
    /// </summary>
    public void Undo()
    {
        _currentSegments = _history.Undo(_currentSegments);
    }

    /// <summary>
    /// Redo last undone operation
    /// </summary>
    public void Redo()
    {
        _currentSegments = _history.Redo(_currentSegments);
    }

    /// <summary>
    /// Get segment containing virtual time
    /// </summary>
    public VideoSegment? GetSegmentAtVirtualTime(TimeSpan virtualTime)
    {
        // Validate input
        if (virtualTime < TimeSpan.Zero)
        {
            return null;
        }

        // Accumulate virtual time to find which segment contains the requested time
        TimeSpan accumulatedVirtual = TimeSpan.Zero;

        foreach (var segment in _currentSegments.KeptSegments)
        {
            var segmentDuration = segment.Duration;
            var nextAccumulated = accumulatedVirtual + segmentDuration;

            // Check if virtualTime falls within this segment's virtual range
            if (virtualTime >= accumulatedVirtual && virtualTime < nextAccumulated)
            {
                return segment;
            }

            accumulatedVirtual = nextAccumulated;
        }

        // Check if at exact end of timeline
        if (virtualTime == accumulatedVirtual && _currentSegments.KeptSegments.Count > 0)
        {
            return _currentSegments.KeptSegments[_currentSegments.KeptSegments.Count - 1];
        }

        // Beyond end or no segments
        return null;
    }

    /// <summary>
    /// Get segment containing source time
    /// </summary>
    public VideoSegment? GetSegmentAtSourceTime(TimeSpan sourceTime)
    {
        // Validate input
        if (sourceTime < TimeSpan.Zero)
        {
            return null;
        }

        // Iterate through segments to find one that contains the source time
        foreach (var segment in _currentSegments.KeptSegments)
        {
            if (segment.Contains(sourceTime))
            {
                return segment;
            }
        }

        // Source time not found in any kept segment (must be in deleted region)
        return null;
    }
}
