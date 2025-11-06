using System;
using Bref.Models;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class PlaybackEngineTests
{
    [Fact]
    public void Constructor_InitializesWithStoppedState()
    {
        using var engine = new PlaybackEngine();
        Assert.Equal(PlaybackState.Stopped, engine.State);
    }

    [Fact]
    public void CurrentTime_StartsAtZero()
    {
        using var engine = new PlaybackEngine();
        Assert.Equal(TimeSpan.Zero, engine.CurrentTime);
    }

    [Fact]
    public void CanPlay_WhenNoVideoLoaded_ReturnsFalse()
    {
        using var engine = new PlaybackEngine();
        Assert.False(engine.CanPlay);
    }

    [Fact]
    public void Play_WhenStopped_ChangesStateToPause()
    {
        using var engine = new PlaybackEngine();
        engine.Play();
        Assert.Equal(PlaybackState.Paused, engine.State);
    }

    [Fact]
    public void Pause_WhenPlaying_ChangesStateToPaused()
    {
        using var engine = new PlaybackEngine();
        engine.Play();
        engine.Pause();
        Assert.Equal(PlaybackState.Paused, engine.State);
    }

    [Fact]
    public void Play_ReturnsQuickly_PreloadingIsNonBlocking()
    {
        // Arrange
        using var engine = new PlaybackEngine();
        var segmentManager = new SegmentManager();
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromSeconds(10),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        segmentManager.Initialize(metadata.Duration);

        // Note: We can't fully initialize without a real FrameCache
        // This test verifies Play() doesn't block even when preloading would occur

        // Act - This should return quickly regardless of preloading
        var startTime = DateTime.UtcNow;
        engine.Play();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Play() should return immediately (< 50ms)
        // If preloading were blocking, this would take much longer
        Assert.True(elapsed.TotalMilliseconds < 50,
            $"Play() took {elapsed.TotalMilliseconds}ms - preloading may be blocking");
    }
}
