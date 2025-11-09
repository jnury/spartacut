using System;
using System.Collections.Generic;
using System.IO;
using SpartaCut.Core.Models;
using FFMpegCore;
using Serilog;
using SkiaSharp;

namespace SpartaCut.Core.Services;

/// <summary>
/// Generates video thumbnails at regular intervals using FFMpegCore.
/// </summary>
public class ThumbnailGenerator
{
    /// <summary>
    /// Generates thumbnails from video at specified intervals.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="interval">Time interval between thumbnails</param>
    /// <param name="width">Thumbnail width in pixels</param>
    /// <param name="height">Thumbnail height in pixels</param>
    /// <returns>List of thumbnail data</returns>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    public List<ThumbnailData> Generate(
        string videoFilePath,
        TimeSpan interval,
        int width,
        int height)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        var thumbnails = new List<ThumbnailData>();

        try
        {
            // Get video duration
            var mediaInfo = FFProbe.Analyse(videoFilePath);
            var duration = mediaInfo.Duration;

            // Generate thumbnails at intervals
            var currentTime = TimeSpan.Zero;
            while (currentTime < duration)
            {
                var thumbnail = GenerateSingle(videoFilePath, currentTime, width, height);
                if (thumbnail != null)
                {
                    thumbnails.Add(thumbnail);
                }

                currentTime += interval;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate thumbnails for: {FilePath}", videoFilePath);
            throw new InvalidDataException($"Failed to generate thumbnails: {ex.Message}", ex);
        }

        return thumbnails;
    }

    /// <summary>
    /// Generates a single thumbnail at a specific time.
    /// </summary>
    public ThumbnailData? GenerateSingle(string videoFilePath, TimeSpan time, int width, int height)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        try
        {
            // Create temporary file for snapshot
            var tempFile = Path.Combine(Path.GetTempPath(), $"thumb_{Guid.NewGuid()}.png");

            try
            {
                // Extract frame using FFMpeg.Snapshot
                var success = FFMpeg.Snapshot(videoFilePath, tempFile, new System.Drawing.Size(width, height), time);

                if (!success || !File.Exists(tempFile))
                {
                    Log.Warning("Failed to extract frame at {Time} from {FilePath}", time, videoFilePath);
                    return null;
                }

                // Load PNG and convert to JPEG
                using var bitmap = SKBitmap.Decode(tempFile);
                if (bitmap == null)
                {
                    Log.Warning("Failed to decode thumbnail image at {Time}", time);
                    return null;
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

                return new ThumbnailData
                {
                    TimePosition = time,
                    ImageData = data.ToArray(),
                    Width = width,
                    Height = height
                };
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate single thumbnail at {Time} for: {FilePath}", time, videoFilePath);
            return null;
        }
    }
}
