using FFmpeg.AutoGen;
using Serilog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Bref.FFmpeg;

/// <summary>
/// Configures FFmpeg.AutoGen to locate native FFmpeg libraries.
/// Handles platform-specific library paths (macOS Homebrew, Windows bundled).
/// </summary>
public static class FFmpegSetup
{
    private static bool _isInitialized = false;

    /// <summary>
    /// Initialize FFmpeg.AutoGen with platform-specific library paths.
    /// Must be called before any FFmpeg operations.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown if FFmpeg libraries cannot be found.</exception>
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        var libraryPath = GetFFmpegLibraryPath();
        Log.Information("Setting FFmpeg library path: {LibraryPath}", libraryPath);

        // Configure FFmpeg.AutoGen to load libraries from the specified path
        ffmpeg.RootPath = libraryPath;

        // Suppress FFmpeg warnings (like "No accelerated colorspace conversion")
        // Only show errors and fatal messages
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);

        // Verify FFmpeg is accessible by checking version
        try
        {
            var version = ffmpeg.av_version_info();
            Log.Information("FFmpeg initialized successfully. Version: {Version}", version);
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize FFmpeg");
            throw new PlatformNotSupportedException(
                $"FFmpeg libraries not found or incompatible. Path: {libraryPath}", ex);
        }
    }

    /// <summary>
    /// Get FFmpeg version string.
    /// </summary>
    /// <returns>FFmpeg version information.</returns>
    public static string GetFFmpegVersion()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("FFmpeg not initialized. Call Initialize() first.");
        }

        return ffmpeg.av_version_info();
    }

    /// <summary>
    /// Detect platform-specific FFmpeg library path.
    /// macOS: /opt/homebrew/opt/ffmpeg@7/lib (Apple Silicon) or /usr/local/opt/ffmpeg@7/lib (Intel)
    /// Windows: Will be bundled in assets/ffmpeg/ (future implementation)
    /// </summary>
    private static string GetFFmpegLibraryPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Try FFmpeg 7 (compatible with FFmpeg.AutoGen 7.x) first - Apple Silicon
            var appleSiliconV7 = "/opt/homebrew/opt/ffmpeg@7/lib";
            if (Directory.Exists(appleSiliconV7))
            {
                Log.Debug("Detected Apple Silicon Homebrew FFmpeg@7 path");
                return appleSiliconV7;
            }

            // Try FFmpeg 7 - Intel
            var intelV7 = "/usr/local/opt/ffmpeg@7/lib";
            if (Directory.Exists(intelV7))
            {
                Log.Debug("Detected Intel Homebrew FFmpeg@7 path");
                return intelV7;
            }

            // Fallback to default FFmpeg - Apple Silicon
            var appleSilicon = "/opt/homebrew/opt/ffmpeg/lib";
            if (Directory.Exists(appleSilicon))
            {
                Log.Debug("Detected Apple Silicon Homebrew FFmpeg path");
                return appleSilicon;
            }

            // Fallback to default FFmpeg - Intel
            var intel = "/usr/local/opt/ffmpeg/lib";
            if (Directory.Exists(intel))
            {
                Log.Debug("Detected Intel Homebrew FFmpeg path");
                return intel;
            }

            throw new PlatformNotSupportedException(
                "FFmpeg not found via Homebrew. Install with: brew install ffmpeg@7");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: Week 9 - Bundle FFmpeg binaries in assets/ffmpeg/
            throw new NotImplementedException(
                "Windows FFmpeg bundling not implemented yet (Week 9 task)");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Not a target platform for MVP, but add basic support
            var linuxPath = "/usr/lib/x86_64-linux-gnu";
            if (Directory.Exists(linuxPath))
            {
                Log.Debug("Detected Linux FFmpeg path");
                return linuxPath;
            }

            throw new PlatformNotSupportedException(
                "FFmpeg not found on Linux. Install with: sudo apt install ffmpeg libavcodec-dev libavformat-dev");
        }

        throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
    }
}
