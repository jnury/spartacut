using System;
using Xunit;
using Bref.Models;
using Bref.Services;

namespace Bref.Tests.Services;

public class VlcPlaybackEngineTests
{
    [Fact]
    public void Constructor_InitializesLibVLC()
    {
        // Act
        using var engine = new VlcPlaybackEngine();

        // Assert - Should not throw
        Assert.NotNull(engine);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var engine = new VlcPlaybackEngine();

        // Act & Assert - Should not throw
        engine.Dispose();
    }

    [Fact]
    public void State_InitiallyIsStopped()
    {
        using var engine = new VlcPlaybackEngine();
        Assert.Equal(PlaybackState.Stopped, engine.State);
    }

    [Fact]
    public void CurrentTime_InitiallyIsZero()
    {
        using var engine = new VlcPlaybackEngine();
        Assert.Equal(TimeSpan.Zero, engine.CurrentTime);
    }

    [Fact]
    public void Initialize_WithValidPath_LoadsMedia()
    {
        using var engine = new VlcPlaybackEngine();
        var segmentManager = new SegmentManager();
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(5),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        // Act
        engine.Initialize(metadata.FilePath, segmentManager, metadata);

        // Assert
        Assert.True(engine.CanPlay);
    }

    [Fact]
    public void CanPlay_BeforeInitialize_ReturnsFalse()
    {
        using var engine = new VlcPlaybackEngine();
        Assert.False(engine.CanPlay);
    }

    [Fact]
    public void Play_WhenInitialized_ChangesStateToPlaying()
    {
        using var engine = new VlcPlaybackEngine();
        var segmentManager = new SegmentManager();
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(5),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };
        segmentManager.Initialize(metadata.Duration);
        engine.Initialize(metadata.FilePath, segmentManager, metadata);

        // Act
        engine.Play();

        // Assert
        Assert.Equal(PlaybackState.Playing, engine.State);
    }

    [Fact]
    public void Pause_WhenPlaying_ChangesStateToPaused()
    {
        using var engine = new VlcPlaybackEngine();
        var segmentManager = new SegmentManager();
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(5),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };
        segmentManager.Initialize(metadata.Duration);
        engine.Initialize(metadata.FilePath, segmentManager, metadata);
        engine.Play();

        // Act
        engine.Pause();

        // Assert
        Assert.Equal(PlaybackState.Paused, engine.State);
    }
}
