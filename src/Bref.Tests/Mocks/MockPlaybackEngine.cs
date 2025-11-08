using System;
using Bref.Core.Models;
using Bref.Core.Services;
using Bref.Core.Services.Interfaces;

namespace Bref.Tests.Mocks;

/// <summary>
/// Mock implementation of IPlaybackEngine for testing
/// </summary>
public class MockPlaybackEngine : IPlaybackEngine
{
    private float _volume = 1.0f;

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public TimeSpan CurrentTime { get; private set; } = TimeSpan.Zero;
    public bool CanPlay { get; private set; } = false;
    public float Volume => _volume;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? TimeChanged;

    public void Initialize(string videoFilePath, SegmentManager segmentManager, VideoMetadata metadata)
    {
        CanPlay = true;
    }

    public void Play()
    {
        State = PlaybackState.Playing;
        StateChanged?.Invoke(this, State);
    }

    public void Pause()
    {
        State = PlaybackState.Paused;
        StateChanged?.Invoke(this, State);
    }

    public void Seek(TimeSpan position)
    {
        CurrentTime = position;
        TimeChanged?.Invoke(this, CurrentTime);
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
    }

    public void Dispose()
    {
        // Nothing to dispose in mock
    }
}
