using System;
using System.IO;
using System.Threading.Tasks;
using Bref.Core.Services;
using Xunit;

namespace Bref.Tests.Services;

public class AudioExtractorTests
{
    [Fact]
    public async Task ExtractAudio_WithValidMP4_CreatesWAVFile()
    {
        // Arrange
        var extractor = new AudioExtractor();
        var videoPath = Path.Combine(
            Path.GetDirectoryName(typeof(AudioExtractorTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "samples", "sample-30s.mp4");
        videoPath = Path.GetFullPath(videoPath);

        // Act
        var audioPath = await extractor.ExtractAudioAsync(videoPath);

        // Assert
        Assert.True(File.Exists(audioPath));
        Assert.EndsWith(".wav", audioPath);

        // Cleanup
        if (File.Exists(audioPath))
            File.Delete(audioPath);
    }

    [Fact]
    public async Task ExtractAudio_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var extractor = new AudioExtractor();
        var videoPath = "non-existent.mp4";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => extractor.ExtractAudioAsync(videoPath));
    }

    [Fact]
    public void Constructor_Succeeds()
    {
        // Act
        var extractor = new AudioExtractor();

        // Assert
        Assert.NotNull(extractor);
    }
}
