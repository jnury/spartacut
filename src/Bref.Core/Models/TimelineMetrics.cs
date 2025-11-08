using System;

namespace Bref.Core.Models;

/// <summary>
/// Metrics and dimensions for timeline rendering calculations.
/// </summary>
public record TimelineMetrics
{
    /// <summary>
    /// Total duration of the video.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Width of the timeline in pixels.
    /// </summary>
    public required double TimelineWidth { get; init; }

    /// <summary>
    /// Height of the timeline in pixels.
    /// </summary>
    public required double TimelineHeight { get; init; }

    /// <summary>
    /// Pixels per second ratio for timeline scaling.
    /// </summary>
    public double PixelsPerSecond => TimelineWidth / TotalDuration.TotalSeconds;

    /// <summary>
    /// Converts time position to pixel position on timeline.
    /// </summary>
    public double TimeToPixel(TimeSpan time) => time.TotalSeconds * PixelsPerSecond;

    /// <summary>
    /// Converts pixel position to time position.
    /// </summary>
    public TimeSpan PixelToTime(double pixel) => TimeSpan.FromSeconds(pixel / PixelsPerSecond);
}
