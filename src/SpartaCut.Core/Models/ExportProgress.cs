using System;

namespace SpartaCut.Core.Models;

/// <summary>
/// Progress information for export operation
/// </summary>
public record ExportProgress
{
    /// <summary>
    /// Current export stage
    /// </summary>
    public ExportStage Stage { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Progress message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Time elapsed since export started
    /// </summary>
    public TimeSpan? ElapsedTime { get; init; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current FFmpeg frame being processed
    /// </summary>
    public long? CurrentFrame { get; init; }

    /// <summary>
    /// Total frames to process
    /// </summary>
    public long? TotalFrames { get; init; }
}

/// <summary>
/// Export operation stages
/// </summary>
public enum ExportStage
{
    Preparing,      // Building FFmpeg command
    Encoding,       // FFmpeg is running
    Finalizing,     // Post-processing
    Complete,       // Export succeeded
    Cancelled,      // User cancelled
    Failed          // Export failed
}
