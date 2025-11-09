using System;
using System.IO;
using System.Linq;
using System.Timers;
using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using SpartaCut.Core.Services.Interfaces;
using LibVLCSharp.Shared;
using Serilog;

namespace SpartaCut.Services;

/// <summary>
/// Manages video playback using LibVLC
/// </summary>
public class VlcPlaybackEngine : IPlaybackEngine
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private SegmentManager? _segmentManager;
    private VideoMetadata? _metadata;
    private Timer? _positionMonitor;
    private PlaybackState _state = PlaybackState.Stopped;
    private DateTime _lastSeekTime = DateTime.MinValue;
    private TimeSpan _lastSeekTarget = TimeSpan.MinValue;
    private bool _isSeeking = false;
    private const int SeekThrottleMs = 50;
    private const int PositionMonitorMs = 50; // Monitor every 50ms
    private bool _disposed = false;
    private float _volume = 1.0f;

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
    /// Current audio volume (0.0 to 1.0)
    /// </summary>
    public float Volume => _volume;

    /// <summary>
    /// Gets the underlying MediaPlayer for binding to UI
    /// </summary>
    public MediaPlayer? MediaPlayer => _mediaPlayer;

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
            // Initialize LibVLCSharp Core with path to VLC libraries
            if (OperatingSystem.IsMacOS())
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var libPath = Path.Combine(baseDir, "lib");
                LibVLCSharp.Shared.Core.Initialize(libPath);
                Log.Information("LibVLC Core initialized with library path: {LibPath}", libPath);
            }
            else
            {
                LibVLCSharp.Shared.Core.Initialize();
                Log.Information("LibVLC Core initialized");
            }

            // Create LibVLC instance
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            // Initialize position monitor
            _positionMonitor = new Timer(PositionMonitorMs);
            _positionMonitor.Elapsed += OnPositionMonitorTick;
            _positionMonitor.AutoReset = true;

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
        if (_libVLC == null) throw new InvalidOperationException("LibVLC not initialized");
        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException("Video file not found", videoFilePath);

        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        try
        {
            _media = new Media(_libVLC, videoFilePath, FromType.FromPath);
            _mediaPlayer!.Media = _media;

            // Parse media to get track info
            _media.Parse(MediaParseOptions.ParseLocal);

            // Set initial volume
            _mediaPlayer.Volume = (int)(_volume * 100);

            // Log media info
            Log.Information("Media loaded: Video tracks: {Video}, Audio tracks: {Audio}",
                _media.Tracks.Count(t => t.TrackType == TrackType.Video),
                _media.Tracks.Count(t => t.TrackType == TrackType.Audio));

            // Set starting position to 0 (VLC will show a black frame until Play is called)
            _mediaPlayer.Time = 0;

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

        // Log audio state for debugging
        if (_mediaPlayer.AudioTrackCount > 0)
        {
            Log.Information("Audio playback started: {Tracks} track(s), Volume: {Volume}%",
                _mediaPlayer.AudioTrackCount, _mediaPlayer.Volume);
        }
        else
        {
            Log.Warning("No audio tracks found in media");
        }

        // Start position monitor
        _positionMonitor!.Start();

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

        // Stop position monitor
        _positionMonitor!.Stop();

        _mediaPlayer!.Pause();
        _state = PlaybackState.Paused;
        StateChanged?.Invoke(this, _state);

        Log.Information("Playback paused at {Time}", CurrentTime);
    }

    /// <summary>
    /// Set audio volume
    /// </summary>
    public void SetVolume(float volume)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));

        // Clamp to valid range
        _volume = Math.Clamp(volume, 0.0f, 1.0f);

        if (_mediaPlayer != null)
        {
            // LibVLC volume is 0-100
            _mediaPlayer.Volume = (int)(_volume * 100);
            Log.Debug("Volume set to {Volume}%", (int)(_volume * 100));
        }
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
        _lastSeekTarget = position;
        _isSeeking = true;

        // Clamp position to valid range
        var clampedMs = Math.Clamp(position.TotalMilliseconds, 0, _metadata!.Duration.TotalMilliseconds);
        _mediaPlayer!.Time = (long)clampedMs;

        // Clear seeking flag after 150ms to give VLC time to complete the seek
        System.Threading.Tasks.Task.Delay(150).ContinueWith(_ => _isSeeking = false);

        Log.Debug("Seeked to {Time}", position);
    }

    private void OnPositionMonitorTick(object? sender, ElapsedEventArgs e)
    {
        // Only monitor when playing
        if (_state != PlaybackState.Playing || _isSeeking || _mediaPlayer == null || _segmentManager == null)
        {
            return;
        }

        var currentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);

        // Check if current position is in a deleted segment
        var virtualTime = _segmentManager.CurrentSegments.SourceToVirtualTime(currentTime);

        if (virtualTime == null)
        {
            // We're in a deleted segment - find next kept segment
            var nextSegment = _segmentManager.CurrentSegments.KeptSegments
                .FirstOrDefault(s => s.SourceStart > currentTime);

            if (nextSegment != null)
            {
                // Jump to next kept segment (add 100ms buffer to ensure we're clearly inside the kept segment)
                var targetTime = nextSegment.SourceStart.Add(TimeSpan.FromMilliseconds(100));

                // Only seek if this is a different target than our last seek (prevents infinite loop)
                if (Math.Abs((targetTime - _lastSeekTarget).TotalMilliseconds) > 50)
                {
                    Log.Information("Skipping deleted segment, jumping to {Time}", targetTime);

                    // Check if audio will stay synchronized
                    if (_mediaPlayer.AudioTrackCount > 0)
                    {
                        Log.Debug("Audio seek: current audio time before seek");
                    }

                    Seek(targetTime);
                }
            }
            else
            {
                // No more segments - end of video
                Log.Information("Playback reached end");
                Pause();
                _state = PlaybackState.Stopped;
                StateChanged?.Invoke(this, _state);
            }
        }

        // Raise time changed event
        TimeChanged?.Invoke(this, currentTime);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _positionMonitor?.Stop();
        _positionMonitor?.Dispose();
        _media?.Dispose();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        _disposed = true;
    }
}
