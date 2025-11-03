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

        // Report progress: Validating
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.Validating,
            Percentage = 10,
            Message = "Validating video format..."
        });

        await Task.Delay(1, cancellationToken); // Make it actually async

        // TODO: Extract metadata
        // TODO: Generate waveform

        throw new NotImplementedException("LoadVideoAsync not fully implemented yet");
    }
}
