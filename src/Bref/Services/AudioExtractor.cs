using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Bref.FFmpeg;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Extracts audio from MP4 videos to WAV format using FFmpeg
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

        // Initialize FFmpeg
        FFmpegSetup.Initialize();

        // Create temp WAV file path
        var tempWavPath = Path.Combine(Path.GetTempPath(), $"bref_audio_{Guid.NewGuid()}.wav");

        try
        {
            // Extract audio using FFmpeg
            // -i input.mp4 -vn -acodec pcm_s16le -ar 44100 -ac 2 output.wav
            var ffmpegPath = FFmpegSetup.GetFFmpegPath();
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoFilePath}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{tempWavPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg process");
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log.Error("FFmpeg audio extraction failed: {Error}", stderr);
                throw new InvalidOperationException($"FFmpeg extraction failed with exit code {process.ExitCode}");
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
