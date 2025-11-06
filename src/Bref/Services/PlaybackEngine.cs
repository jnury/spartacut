using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Bref.Models;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Manages video playback state and frame scheduling
/// </summary>
public class PlaybackEngine : IDisposable
{
    private const int PreloadFrameCount = 10; // Preload 10 frames ahead (~333ms at 30fps)

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
    /// Initializes playback with video and audio resources
    /// </summary>
    public async Task InitializeAsync(FrameCache frameCache, SegmentManager segmentManager,
        VideoMetadata metadata, AudioExtractor audioExtractor)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlaybackEngine));

        _frameCache = frameCache ?? throw new ArgumentNullException(nameof(frameCache));
        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _duration = metadata.Duration;
        _frameRate = metadata.FrameRate;

        // Update frame timer interval based on frame rate
        _frameTimer.Interval = 1000.0 / _frameRate;

        // Extract and load audio
        try
        {
            Log.Information("Extracting audio from video...");
            var audioPath = await audioExtractor.ExtractAudioAsync(metadata.FilePath);

            if (_audioPlayer == null)
            {
                _audioPlayer = new AudioPlayer();
            }

            await _audioPlayer.LoadAudioAsync(audioPath);

            Log.Information("PlaybackEngine initialized: Duration={Duration}, FrameRate={FrameRate}fps, Audio loaded",
                _duration, _frameRate);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load audio, continuing without audio playback");
            // Continue without audio - video playback will still work
        }
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

        // Only reset to beginning if at the very end (not if user seeked to middle)
        if (_currentTime >= _duration)
        {
            _currentTime = TimeSpan.Zero;
            TimeChanged?.Invoke(this, _currentTime);
            Log.Debug("Playback at end, reset to beginning");
        }

        _state = PlaybackState.Playing;
        StateChanged?.Invoke(this, _state);

        // Start preloading frames ahead (non-blocking)
        PreloadFrames(_currentTime);

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

        // Guard against race condition - if already at end, don't process
        if (_currentTime >= _duration)
        {
            return;
        }

        // Advance time by one frame
        var frameTime = TimeSpan.FromSeconds(1.0 / _frameRate);
        var nextTime = _currentTime + frameTime;

        // Check if next time would exceed duration - stop before advancing
        if (nextTime >= _duration)
        {
            _currentTime = _duration;
            Pause();
            TimeChanged?.Invoke(this, _currentTime);
            Log.Information("Playback reached end");
            return;
        }

        // Check if next time is in a deleted segment
        var virtualTime = _segmentManager.CurrentSegments.SourceToVirtualTime(nextTime);
        if (virtualTime == null)
        {
            // We're in a deleted segment - skip to next kept segment
            Log.Debug("Skipping deleted segment at {Time}", nextTime);

            // Find next kept segment
            var nextKeptSegment = _segmentManager.CurrentSegments.KeptSegments
                .FirstOrDefault(s => s.SourceStart > nextTime);

            if (nextKeptSegment != null)
            {
                // Jump to start of next segment
                _currentTime = nextKeptSegment.SourceStart;
                Log.Information("Jumped to next segment at {Time}", _currentTime);
            }
            else
            {
                // No more segments - end of video
                _currentTime = _duration;
                Pause();
                TimeChanged?.Invoke(this, _currentTime);
                Log.Information("Playback reached end (after deleted segment)");
                return;
            }
        }
        else
        {
            // Normal playback - advance time
            _currentTime = nextTime;
        }

        // Clamp to duration before firing event (safety check for race conditions)
        if (_currentTime > _duration)
        {
            _currentTime = _duration;
        }

        // Notify time changed
        TimeChanged?.Invoke(this, _currentTime);
    }

    /// <summary>
    /// Preloads frames ahead of current time for smooth playback.
    /// Runs asynchronously without blocking.
    /// </summary>
    private void PreloadFrames(TimeSpan fromTime)
    {
        if (_frameCache == null || _segmentManager == null)
        {
            return;
        }

        // Run preloading in background (non-blocking)
        Task.Run(() =>
        {
            try
            {
                var frameTime = TimeSpan.FromSeconds(1.0 / _frameRate);

                for (int i = 1; i <= PreloadFrameCount; i++)
                {
                    var preloadTime = fromTime + (frameTime * i);

                    // Stop if we've reached the end of the video
                    if (preloadTime >= _duration)
                    {
                        Log.Debug("Preloading stopped at end of video");
                        break;
                    }

                    // This will populate the cache if not already cached
                    _frameCache.GetFrame(preloadTime);

                    Log.Verbose("Preloaded frame at {Time}", preloadTime);
                }

                Log.Debug("Preloaded {Count} frames ahead from {Time}", PreloadFrameCount, fromTime);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to preload frames from {Time}", fromTime);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _frameTimer?.Dispose();
        _audioPlayer?.Dispose();
        _disposed = true;
    }
}
