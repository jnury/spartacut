using System;

namespace Bref.Core.Models;

/// <summary>
/// Represents a continuous portion of the source video (kept segment)
/// </summary>
public class VideoSegment
{
    /// <summary>
    /// Start position in source video file
    /// </summary>
    public TimeSpan SourceStart { get; set; }

    /// <summary>
    /// End position in source video file
    /// </summary>
    public TimeSpan SourceEnd { get; set; }

    /// <summary>
    /// Duration of this segment
    /// </summary>
    public TimeSpan Duration => SourceEnd - SourceStart;

    /// <summary>
    /// Check if this segment contains a source timestamp
    /// </summary>
    public bool Contains(TimeSpan sourceTime) =>
        sourceTime >= SourceStart && sourceTime <= SourceEnd;
}
