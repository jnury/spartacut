using System;
using System.IO;
using System.Threading.Tasks;
using Bref.Core.Services;
using Xunit;

namespace Bref.Tests.Services;

public class AudioPlayerTests
{
    [Fact]
    public void Constructor_InitializesWithFullVolume()
    {
        var player = new AudioPlayer();
        Assert.Equal(1.0f, player.Volume);
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

    [Fact]
    public async Task LoadAudio_WithValidWAV_LoadsSuccessfully()
    {
        // Arrange
        var player = new AudioPlayer();
        var videoPath = Path.Combine(
            Path.GetDirectoryName(typeof(AudioPlayerTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "samples", "sample-30s.mp4");
        videoPath = Path.GetFullPath(videoPath);

        // Extract audio from video first
        var extractor = new AudioExtractor();
        var wavPath = await extractor.ExtractAudioAsync(videoPath);

        try
        {
            // Act
            await player.LoadAudioAsync(wavPath);

            // Assert
            Assert.True(player.IsLoaded);
        }
        finally
        {
            // Cleanup
            player.Dispose();
            if (File.Exists(wavPath))
                File.Delete(wavPath);
        }
    }

    [Fact]
    public async Task LoadAudio_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var player = new AudioPlayer();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => player.LoadAudioAsync("non-existent.wav"));
    }

    [Fact]
    public void IsLoaded_WhenNoAudioLoaded_ReturnsFalse()
    {
        // Arrange
        var player = new AudioPlayer();

        // Assert
        Assert.False(player.IsLoaded);
    }
}
