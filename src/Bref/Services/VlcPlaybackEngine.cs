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
    private Media? _media;
    private SegmentManager? _segmentManager;
    private VideoMetadata? _metadata;
    private PlaybackState _state = PlaybackState.Stopped;
    private DateTime _lastSeekTime = DateTime.MinValue;
    private bool _isSeeking = false;
    private const int SeekThrottleMs = 50;
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
    /// Whether playback can start (video loaded)
    /// </summary>
    public bool CanPlay => _media != null && _segmentManager != null;

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

    /// <summary>
    /// Initialize playback with video file and segment manager
    /// </summary>
    public void Initialize(string videoFilePath, SegmentManager segmentManager, VideoMetadata metadata)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));
        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found", videoFilePath);

        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        try
        {
            _media = new Media(_libVLC, videoFilePath, FromType.FromPath);
            _mediaPlayer!.Media = _media;

            Log.Information("VlcPlaybackEngine initialized with {FilePath}", videoFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load media from {FilePath}", videoFilePath);
            throw new InvalidOperationException(
                "VLC cannot play this video. The file may be corrupted or in an unsupported format.", ex);
        }
    }

    /// <summary>
    /// Start playback
    /// </summary>
    public void Play()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));

        if (!CanPlay)
        {
            Log.Warning("Cannot play: No video loaded");
            return;
        }

        if (_state == PlaybackState.Playing)
        {
            Log.Debug("Already playing");
            return;
        }

        _mediaPlayer!.Play();
        _state = PlaybackState.Playing;
        StateChanged?.Invoke(this, _state);

        Log.Information("Playback started");
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));

        if (_state != PlaybackState.Playing)
        {
            return;
        }

        _mediaPlayer!.Pause();
        _state = PlaybackState.Paused;
        StateChanged?.Invoke(this, _state);

        Log.Information("Playback paused at {Time}", CurrentTime);
    }

    /// <summary>
    /// Seek to specific time with throttling
    /// </summary>
    public void Seek(TimeSpan position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));

        if (!CanPlay)
        {
            Log.Warning("Cannot seek: No video loaded");
            return;
        }

        // Throttle seeks to prevent overwhelming VLC
        var now = DateTime.UtcNow;
        var timeSinceLastSeek = (now - _lastSeekTime).TotalMilliseconds;

        if (timeSinceLastSeek < SeekThrottleMs)
        {
            // Too soon - skip this seek
            Log.Debug("Seek throttled");
            return;
        }

        _lastSeekTime = now;
        _isSeeking = true;

        // Clamp position to valid range
        var clampedMs = Math.Clamp(position.TotalMilliseconds, 0, _metadata!.Duration.TotalMilliseconds);
        _mediaPlayer!.Time = (long)clampedMs;

        // Clear seeking flag after 50ms
        System.Threading.Tasks.Task.Delay(50).ContinueWith(_ => _isSeeking = false);

        Log.Debug("Seeked to {Time}", position);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        _disposed = true;
    }
}
