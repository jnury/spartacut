using FFMpegCore;
using SpartaCut.Core.Models;
using Serilog;
using System;
using System.IO;

namespace SpartaCut.Core.FFmpeg;

/// <summary>
/// Extracts metadata from video files using FFMpegCore.
/// No disposal needed - FFMpegCore shells out to external ffmpeg process.
/// </summary>
public class FrameExtractor
{

    /// <summary>
    /// Extract metadata from a video file.
    /// </summary>
    /// <param name="filePath">Path to video file.</param>
    /// <returns>Video metadata.</returns>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidOperationException">Failed to parse video file.</exception>
    public VideoMetadata ExtractMetadata(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Video file not found: {filePath}");
        }

        Log.Information("Extracting metadata from: {FilePath}", filePath);

        try
        {
            // Use FFProbe to analyze video file
            var mediaInfo = FFProbe.Analyse(filePath);

            if (mediaInfo.PrimaryVideoStream == null)
            {
                throw new InvalidOperationException("No video stream found in file");
            }

            var videoStream = mediaInfo.PrimaryVideoStream;

            // Get file size
            var fileInfo = new FileInfo(filePath);

            var metadata = new VideoMetadata
            {
                FilePath = filePath,
                Duration = mediaInfo.Duration,
                Width = videoStream.Width,
                Height = videoStream.Height,
                FrameRate = videoStream.FrameRate,
                CodecName = videoStream.CodecName,
                PixelFormat = videoStream.PixelFormat,
                Bitrate = mediaInfo.PrimaryVideoStream.BitRate,
                FileSizeBytes = fileInfo.Length
            };

            Log.Information("Metadata extracted: {Metadata}", metadata);

            return metadata;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            Log.Error(ex, "Failed to extract metadata from {FilePath}", filePath);
            throw new InvalidOperationException($"Failed to parse video file: {ex.Message}", ex);
        }
    }
}
