using Bref.FFmpeg;
using Bref.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bref.Services;

/// <summary>
/// Service for video file operations.
/// </summary>
public class VideoService : IVideoService
{
    private static readonly string[] SupportedExtensions = { ".mp4", ".MP4" };

    public async Task<VideoMetadata> LoadVideoAsync(
        string filePath,
        IProgress<LoadProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // Validate file exists
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Video file not found: {filePath}", filePath);
        }

        // Validate extension
        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException(
                $"Video format '{extension}' is not supported. Only MP4/H.264 videos are supported in the MVP.");
        }

        try
        {
            // Report progress: Validating
            progress.Report(new LoadProgress
            {
                Stage = LoadStage.Validating,
                Percentage = 10,
                Message = "Validating video format..."
            });

            await Task.Run(() => Thread.Sleep(100), cancellationToken); // Simulate validation work

            // Report progress: Extracting metadata
            progress.Report(new LoadProgress
            {
                Stage = LoadStage.ExtractingMetadata,
                Percentage = 30,
                Message = "Extracting video metadata..."
            });

            // Extract metadata using FrameExtractor
            VideoMetadata metadata = null!;
            await Task.Run(() =>
            {
                using var extractor = new FrameExtractor();
                metadata = extractor.ExtractMetadata(filePath);
            }, cancellationToken);

            // Validate codec
            if (!metadata.IsSupported())
            {
                throw new NotSupportedException(
                    $"Video codec '{metadata.CodecName}' is not supported. Only H.264 is supported in the MVP.");
            }

            Log.Information("Video metadata extracted: {Metadata}", metadata);

            // TODO: Generate waveform (Task 4)

            // Report progress: Complete
            progress.Report(new LoadProgress
            {
                Stage = LoadStage.Complete,
                Percentage = 100,
                Message = "Video loaded successfully"
            });

            return metadata;
        }
        catch (Exception ex) when (ex is not NotSupportedException and not FileNotFoundException)
        {
            Log.Error(ex, "Failed to load video: {FilePath}", filePath);

            progress.Report(new LoadProgress
            {
                Stage = LoadStage.Failed,
                Percentage = 0,
                Message = $"Failed to load video: {ex.Message}"
            });

            throw new InvalidDataException($"Failed to load video: {ex.Message}", ex);
        }
    }
}
