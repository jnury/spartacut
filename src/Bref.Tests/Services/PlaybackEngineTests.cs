using System;
using System.Threading.Tasks;
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
}
