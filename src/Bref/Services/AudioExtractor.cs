using System;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Extracts audio from MP4 videos to WAV format using FFMpegCore
/// </summary>
public class AudioExtractor
{
    /// <summary>
    /// Extracts audio from video file to temporary WAV file
    /// </summary>
    /// <param name="videoFilePath">Path to input MP4 file</param>
    /// <returns>Path to extracted WAV file in temp directory</returns>
    /// <exception cref="FileNotFoundException">Video file not found</exception>
    /// <exception cref="InvalidOperationException">FFmpeg extraction failed</exception>
    public async Task<string> ExtractAudioAsync(string videoFilePath)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        // Create temp WAV file path
        var tempWavPath = Path.Combine(Path.GetTempPath(), $"bref_audio_{Guid.NewGuid()}.wav");

        try
        {
            // Extract audio using FFMpegCore
            var success = await FFMpegArguments
                .FromFileInput(videoFilePath)
                .OutputToFile(tempWavPath, overwrite: true, options => options
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(44100)
                    .WithCustomArgument("-ac 2")
                    .DisableChannel(FFMpegCore.Enums.Channel.Video))
                .ProcessAsynchronously();

            if (!success)
            {
                throw new InvalidOperationException("FFmpeg extraction failed");
            }

            if (!File.Exists(tempWavPath))
            {
                throw new InvalidOperationException("FFmpeg completed but WAV file not created");
            }

            Log.Information("Extracted audio to {Path}", tempWavPath);
            return tempWavPath;
        }
        catch (Exception ex)
        {
            // Clean up temp file if created
            if (File.Exists(tempWavPath))
            {
                try { File.Delete(tempWavPath); } catch { }
            }

            throw new InvalidOperationException($"Failed to extract audio: {ex.Message}", ex);
        }
    }
}
