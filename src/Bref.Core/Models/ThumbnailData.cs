using System;

namespace Bref.Core.Models;

/// <summary>
/// Represents a single video thumbnail at a specific time position.
/// </summary>
public record ThumbnailData
{
    /// <summary>
    /// Time position in the video where this thumbnail was extracted.
    /// </summary>
    public required TimeSpan TimePosition { get; init; }

    /// <summary>
    /// Thumbnail image data as byte array (JPEG format).
    /// </summary>
    public required byte[] ImageData { get; init; }

    /// <summary>
    /// Width of the thumbnail image in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Height of the thumbnail image in pixels.
    /// </summary>
    public required int Height { get; init; }
}
