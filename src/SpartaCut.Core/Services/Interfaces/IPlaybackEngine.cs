using System;
using SpartaCut.Core.Models;

namespace SpartaCut.Core.Services.Interfaces;

/// <summary>
/// Interface for video playback engines
/// </summary>
public interface IPlaybackEngine : IDisposable
{
    /// <summary>
    /// Current playback state
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Current playback time (source time)
    /// </summary>
    TimeSpan CurrentTime { get; }

    /// <summary>
    /// Whether playback can start (video loaded)
    /// </summary>
    bool CanPlay { get; }

    /// <summary>
    /// Current audio volume (0.0 to 1.0)
    /// </summary>
    float Volume { get; }

    /// <summary>
    /// Event raised when playback state changes
    /// </summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Event raised when playback time changes
    /// </summary>
    event EventHandler<TimeSpan>? TimeChanged;

    /// <summary>
    /// Initialize playback with video file and segment manager
    /// </summary>
    void Initialize(string videoFilePath, SegmentManager segmentManager, VideoMetadata metadata);

    /// <summary>
    /// Start playback
    /// </summary>
    void Play();

    /// <summary>
    /// Pause playback
    /// </summary>
    void Pause();

    /// <summary>
    /// Seek to specific time
    /// </summary>
    void Seek(TimeSpan position);

    /// <summary>
    /// Set audio volume (0.0 to 1.0)
    /// </summary>
    void SetVolume(float volume);
}
