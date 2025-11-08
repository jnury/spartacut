using Bref.Core.Models;
using Xunit;

namespace Bref.Tests.Models;

public class PlaybackStateTests
{
    [Fact]
    public void IsPlaying_WhenPlayingState_ReturnsTrue()
    {
        var state = PlaybackState.Playing;
        Assert.True(state.IsPlaying());
    }

    [Fact]
    public void IsPlaying_WhenPausedState_ReturnsFalse()
    {
        var state = PlaybackState.Paused;
        Assert.False(state.IsPlaying());
    }

    [Fact]
    public void IsPlaying_WhenStoppedState_ReturnsFalse()
    {
        var state = PlaybackState.Stopped;
        Assert.False(state.IsPlaying());
    }
}
