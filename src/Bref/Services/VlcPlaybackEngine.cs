using System;
using System.IO;
using Bref.Models;
using LibVLCSharp.Shared;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Manages video playback using LibVLC
/// </summary>
public class VlcPlaybackEngine : IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private PlaybackState _state = PlaybackState.Stopped;
    private bool _disposed = false;

    /// <summary>
    /// Current playback state
    /// </summary>
    public PlaybackState State => _state;

    /// <summary>
    /// Current playback time (source time)
    /// </summary>
    public TimeSpan CurrentTime
    {
        get
        {
            if (_mediaPlayer == null) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(_mediaPlayer.Time);
        }
    }

    /// <summary>
    /// Event raised when playback state changes
    /// </summary>
    public event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Event raised when playback time changes
    /// </summary>
    public event EventHandler<TimeSpan>? TimeChanged;

    static VlcPlaybackEngine()
    {
        // Static constructor: Set VLC_PLUGIN_PATH before any instance is created
        // This ensures the environment variable is set before LibVLC initialization
        if (OperatingSystem.IsMacOS())
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var pluginPath = Path.Combine(baseDir, "plugins");
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
            Log.Information("Static initialization: Set VLC_PLUGIN_PATH to: {PluginPath}", pluginPath);
        }
    }

    public VlcPlaybackEngine()
    {
        try
        {
            // Initialize LibVLCSharp Core
            Core.Initialize();
            Log.Information("LibVLC Core initialized");

            // Create LibVLC instance
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            Log.Information("LibVLC instance created successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize LibVLC. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            if (ex.InnerException != null)
            {
                Log.Error("Inner exception: {InnerExceptionType}, Message: {InnerMessage}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }
            throw new InvalidOperationException(
                "Video playback unavailable. LibVLC could not initialize.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        _disposed = true;
    }
}
