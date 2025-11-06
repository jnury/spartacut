using System;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class AudioPlayerTests
{
    [Fact]
    public void Constructor_InitializesWithZeroVolume()
    {
        var player = new AudioPlayer();
        Assert.Equal(0f, player.Volume);
    }

    [Fact]
    public void SetVolume_UpdatesVolume()
    {
        var player = new AudioPlayer();
        player.SetVolume(0.5f);
        Assert.Equal(0.5f, player.Volume);
    }

    [Fact]
    public void SetVolume_ClampsToRange()
    {
        var player = new AudioPlayer();

        player.SetVolume(1.5f);
        Assert.Equal(1.0f, player.Volume);

        player.SetVolume(-0.5f);
        Assert.Equal(0.0f, player.Volume);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var player = new AudioPlayer();
        player.Dispose();
        // Should not throw
    }
}
