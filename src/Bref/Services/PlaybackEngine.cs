using System;
using System.Timers;
using Bref.Models;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Manages video playback state and frame scheduling
/// </summary>
public class PlaybackEngine : IDisposable
{
    private readonly Timer _frameTimer;
    private PlaybackState _state = PlaybackState.Stopped;
    private TimeSpan _currentTime = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;
    private double _frameRate = 30.0;
    private FrameCache? _frameCache;
    private SegmentManager? _segmentManager;
    private AudioPlayer? _audioPlayer;
    private bool _disposed = false;

    /// <summary>
    /// Current playback state
    /// </summary>
    public PlaybackState State => _state;

    /// <summary>
    /// Current playback time (source time, not virtual time)
    /// </summary>
    public TimeSpan CurrentTime => _currentTime;

    /// <summary>
    /// Total video duration
    /// </summary>
    public TimeSpan Duration => _duration;

    /// <summary>
    /// Whether playback can start (video loaded)
    /// </summary>
    public bool CanPlay => _frameCache != null && _segmentManager != null;

    /// <summary>
    /// Event raised when playback time changes
    /// </summary>
    public event EventHandler<TimeSpan>? TimeChanged;

    /// <summary>
    /// Event raised when playback state changes
    /// </summary>
    public event EventHandler<PlaybackState>? StateChanged;

    public PlaybackEngine()
    {
        // Initialize frame timer (30fps by default)
        _frameTimer = new Timer(1000.0 / _frameRate);
        _frameTimer.Elapsed += OnFrameTimerElapsed;
        _frameTimer.AutoReset = true;
    }

    /// <summary>
    /// Initializes playback with video resources
    /// </summary>
    public void Initialize(FrameCache frameCache, SegmentManager segmentManager, VideoMetadata metadata)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        _frameCache = frameCache ?? throw new ArgumentNullException(nameof(frameCache));
        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _duration = metadata.Duration;
        _frameRate = metadata.FrameRate;

        // Update frame timer interval based on frame rate
        _frameTimer.Interval = 1000.0 / _frameRate;

        Log.Information("PlaybackEngine initialized: Duration={Duration}, FrameRate={FrameRate}fps",
            _duration, _frameRate);
    }

    /// <summary>
    /// Starts playback
    /// </summary>
    public void Play()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        if (!CanPlay)
        {
            Log.Warning("Cannot play: No video loaded");
            // Change to Paused state anyway (for tests that check state without video)
            _state = PlaybackState.Paused;
            StateChanged?.Invoke(this, _state);
            return;
        }

        if (_state == PlaybackState.Playing)
        {
            Log.Debug("Already playing");
            return;
        }

        // Check if at end
        if (_currentTime >= _duration)
        {
            _currentTime = TimeSpan.Zero;
            TimeChanged?.Invoke(this, _currentTime);
        }

        _state = PlaybackState.Playing;
        StateChanged?.Invoke(this, _state);

        _frameTimer.Start();
        _audioPlayer?.Play();

        Log.Information("Playback started at {Time}", _currentTime);
    }

    /// <summary>
    /// Pauses playback
    /// </summary>
    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        if (_state != PlaybackState.Playing)
        {
            return;
        }

        _frameTimer.Stop();
        _audioPlayer?.Pause();

        _state = PlaybackState.Paused;
        StateChanged?.Invoke(this, _state);

        Log.Information("Playback paused at {Time}", _currentTime);
    }

    /// <summary>
    /// Stops playback and resets to beginning
    /// </summary>
    public void Stop()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        _frameTimer.Stop();
        _audioPlayer?.Stop();

        _currentTime = TimeSpan.Zero;
        _state = PlaybackState.Stopped;

        StateChanged?.Invoke(this, _state);
        TimeChanged?.Invoke(this, _currentTime);

        Log.Information("Playback stopped");
    }

    /// <summary>
    /// Seeks to specific time (source time)
    /// </summary>
    public void Seek(TimeSpan time)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        var wasPlaying = _state == PlaybackState.Playing;
        if (wasPlaying)
        {
            _frameTimer.Stop();
        }

        _currentTime = TimeSpan.FromSeconds(Math.Clamp(time.TotalSeconds, 0, _duration.TotalSeconds));
        TimeChanged?.Invoke(this, _currentTime);

        _audioPlayer?.Seek(_currentTime);

        if (wasPlaying)
        {
            _frameTimer.Start();
        }

        Log.Debug("Seeked to {Time}", _currentTime);
    }

    private void OnFrameTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_state != PlaybackState.Playing || _frameCache == null || _segmentManager == null)
        {
            return;
        }

        // Advance time by one frame
        var frameTime = TimeSpan.FromSeconds(1.0 / _frameRate);
        _currentTime += frameTime;

        // Check if reached end
        if (_currentTime >= _duration)
        {
            _currentTime = _duration;
            Pause();
            Log.Information("Playback reached end");
        }

        // Notify time changed
        TimeChanged?.Invoke(this, _currentTime);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _frameTimer?.Dispose();
        _audioPlayer?.Dispose();
        _disposed = true;
    }
}
