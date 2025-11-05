using System;
using System.Collections.Generic;
using System.Linq;

namespace Bref.Models;

/// <summary>
/// Manages the virtual timeline of kept segments
/// </summary>
public class SegmentList
{
    /// <summary>
    /// Ordered list of kept video segments (non-deleted portions)
    /// Invariant: Segments are non-overlapping and sorted by SourceStart
    /// </summary>
    public List<VideoSegment> KeptSegments { get; set; } = new();

    /// <summary>
    /// Total duration of all kept segments (virtual timeline length)
    /// </summary>
    public TimeSpan TotalDuration =>
        TimeSpan.FromSeconds(KeptSegments.Sum(s => s.Duration.TotalSeconds));

    /// <summary>
    /// Number of segments in virtual timeline
    /// </summary>
    public int SegmentCount => KeptSegments.Count;

    /// <summary>
    /// Deep clone this segment list for undo history
    /// </summary>
    public SegmentList Clone()
    {
        var clone = new SegmentList();
        foreach (var segment in KeptSegments)
        {
            clone.KeptSegments.Add(new VideoSegment
            {
                SourceStart = segment.SourceStart,
                SourceEnd = segment.SourceEnd
            });
        }
        return clone;
    }

    /// <summary>
    /// Convert virtual timeline position to source file position
    /// </summary>
    /// <param name="virtualTime">Position in virtual timeline (after deletions)</param>
    /// <returns>Position in source video file</returns>
    public TimeSpan VirtualToSourceTime(TimeSpan virtualTime)
    {
        // Handle empty list
        if (KeptSegments.Count == 0)
        {
            return TimeSpan.Zero;
        }

        // Accumulate virtual durations to find which segment contains virtualTime
        TimeSpan accumulatedVirtual = TimeSpan.Zero;

        for (int i = 0; i < KeptSegments.Count; i++)
        {
            var segment = KeptSegments[i];
            var segmentDuration = segment.Duration;
            var nextAccumulated = accumulatedVirtual + segmentDuration;

            // Check if virtualTime falls within this segment's virtual range
            // For the last segment, use <= to include the end time
            // For other segments, use < to ensure boundary times go to the next segment
            bool inRange = (i == KeptSegments.Count - 1)
                ? virtualTime <= nextAccumulated
                : virtualTime < nextAccumulated;

            if (inRange)
            {
                // Calculate offset within this segment
                var offset = virtualTime - accumulatedVirtual;
                return segment.SourceStart + offset;
            }

            accumulatedVirtual = nextAccumulated;
        }

        // Beyond end - return last segment's end
        return KeptSegments[KeptSegments.Count - 1].SourceEnd;
    }

    /// <summary>
    /// Convert source file position to virtual timeline position
    /// </summary>
    /// <param name="sourceTime">Position in source video file</param>
    /// <returns>Position in virtual timeline, or null if in deleted region</returns>
    public TimeSpan? SourceToVirtualTime(TimeSpan sourceTime)
    {
        // Accumulate virtual durations while searching for segment containing sourceTime
        TimeSpan accumulatedVirtual = TimeSpan.Zero;

        foreach (var segment in KeptSegments)
        {
            // Check if sourceTime is within this segment
            if (segment.Contains(sourceTime))
            {
                // Calculate offset from segment start
                var offset = sourceTime - segment.SourceStart;
                return accumulatedVirtual + offset;
            }

            // Move to next segment
            accumulatedVirtual += segment.Duration;
        }

        // Source time not found in any kept segment - must be in deleted region
        return null;
    }

    /// <summary>
    /// Delete a portion of the virtual timeline
    /// </summary>
    /// <param name="virtualStart">Start position in virtual timeline</param>
    /// <param name="virtualEnd">End position in virtual timeline</param>
    public void DeleteSegment(TimeSpan virtualStart, TimeSpan virtualEnd)
    {
        // Validate parameters
        if (virtualEnd <= virtualStart)
        {
            throw new ArgumentException("virtualEnd must be greater than virtualStart");
        }

        if (virtualStart >= TotalDuration)
        {
            throw new ArgumentException("virtualStart is beyond the total duration");
        }

        // Convert virtual times to source times
        var sourceStart = VirtualToSourceTime(virtualStart);
        var sourceEnd = VirtualToSourceTime(virtualEnd);

        // Process each segment and determine how it's affected by the deletion
        var newSegments = new List<VideoSegment>();

        foreach (var segment in KeptSegments)
        {
            // Case 1: Segment is completely before deletion range - keep as is
            if (segment.SourceEnd <= sourceStart)
            {
                newSegments.Add(new VideoSegment
                {
                    SourceStart = segment.SourceStart,
                    SourceEnd = segment.SourceEnd
                });
            }
            // Case 2: Segment is completely after deletion range - keep as is
            else if (segment.SourceStart >= sourceEnd)
            {
                newSegments.Add(new VideoSegment
                {
                    SourceStart = segment.SourceStart,
                    SourceEnd = segment.SourceEnd
                });
            }
            // Case 3: Deletion is in the middle of segment - split into two
            else if (sourceStart > segment.SourceStart && sourceEnd < segment.SourceEnd)
            {
                // Keep portion before deletion
                newSegments.Add(new VideoSegment
                {
                    SourceStart = segment.SourceStart,
                    SourceEnd = sourceStart
                });

                // Keep portion after deletion
                newSegments.Add(new VideoSegment
                {
                    SourceStart = sourceEnd,
                    SourceEnd = segment.SourceEnd
                });
            }
            // Case 4: Deletion overlaps start of segment - trim start
            else if (sourceStart <= segment.SourceStart && sourceEnd < segment.SourceEnd)
            {
                newSegments.Add(new VideoSegment
                {
                    SourceStart = sourceEnd,
                    SourceEnd = segment.SourceEnd
                });
            }
            // Case 5: Deletion overlaps end of segment - trim end
            else if (sourceStart > segment.SourceStart && sourceEnd >= segment.SourceEnd)
            {
                newSegments.Add(new VideoSegment
                {
                    SourceStart = segment.SourceStart,
                    SourceEnd = sourceStart
                });
            }
            // Case 6: Deletion completely covers segment - remove it (don't add to newSegments)
        }

        // Replace the kept segments with the new list
        KeptSegments = newSegments;
    }
}
