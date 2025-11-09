using System;

namespace Bref.Core.Models;

/// <summary>
/// Options for video export operation
/// </summary>
public record ExportOptions
{
    /// <summary>
    /// Path to source video file
    /// </summary>
    public required string SourceFilePath { get; init; }

    /// <summary>
    /// Path for output video file
    /// </summary>
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// Segments to export (from SegmentManager)
    /// </summary>
    public required SegmentList Segments { get; init; }

    /// <summary>
    /// Video metadata (codec, resolution, etc.)
    /// </summary>
    public required VideoMetadata Metadata { get; init; }

    /// <summary>
    /// Enable hardware acceleration if available
    /// </summary>
    public bool UseHardwareAcceleration { get; init; } = true;

    /// <summary>
    /// Preferred hardware encoder (null = auto-detect)
    /// </summary>
    public string? PreferredEncoder { get; init; } = null;
}
