using System;

namespace Bref.Core.Models;

/// <summary>
/// Metadata extracted from a video file.
/// </summary>
public record VideoMetadata
{
    /// <summary>
    /// Full path to the video file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Total duration of the video.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Frames per second.
    /// </summary>
    public required double FrameRate { get; init; }

    /// <summary>
    /// Video codec name (e.g., "h264", "hevc").
    /// </summary>
    public required string CodecName { get; init; }

    /// <summary>
    /// Pixel format (e.g., "yuv420p").
    /// </summary>
    public required string PixelFormat { get; init; }

    /// <summary>
    /// Bitrate in bits per second.
    /// </summary>
    public long Bitrate { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Audio waveform data (null if not yet generated).
    /// </summary>
    public WaveformData? Waveform { get; init; }

    /// <summary>
    /// Check if this is a supported format (MP4/H.264 for MVP).
    /// </summary>
    public bool IsSupported()
    {
        // MVP only supports H.264 codec
        return CodecName.Equals("h264", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get human-readable file size.
    /// </summary>
    public string GetFileSizeFormatted()
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return FileSizeBytes switch
        {
            < KB => $"{FileSizeBytes} bytes",
            < MB => $"{FileSizeBytes / (double)KB:F2} KB",
            < GB => $"{FileSizeBytes / (double)MB:F2} MB",
            _ => $"{FileSizeBytes / (double)GB:F2} GB"
        };
    }

    public override string ToString()
    {
        return $"{Width}x{Height} @ {FrameRate:F2}fps, {CodecName}, {Duration:hh\\:mm\\:ss}, {GetFileSizeFormatted()}";
    }
}
