using System;
using System.IO;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class PersistentFrameDecoderTests
{
    private readonly string _testVideoPath;

    public PersistentFrameDecoderTests()
    {
        _testVideoPath = Path.Combine("/Users/jnury-perso/Repositories/Bref/samples", "sample-30s.mp4");
    }

    [Fact]
    public void Constructor_WithValidVideo_OpensSuccessfully()
    {
        // Act & Assert
        using var decoder = new PersistentFrameDecoder(_testVideoPath);
        // Should not throw
    }

    [Fact]
    public void Constructor_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            new PersistentFrameDecoder("nonexistent.mp4"));
    }

    [Fact]
    public void DecodeFrameAt_ReturnsFrame640x360()
    {
        // Arrange
        using var decoder = new PersistentFrameDecoder(_testVideoPath);

        // Act
        var frame = decoder.DecodeFrameAt(TimeSpan.FromSeconds(1.0));

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(640, frame.Width);
        Assert.Equal(360, frame.Height);
        Assert.Equal(640 * 360 * 3, frame.ImageData.Length); // RGB24
    }

    [Fact]
    public void DecodeFrameAt_MultipleCallsWithoutReopening_ReturnsDifferentFrames()
    {
        // Arrange
        using var decoder = new PersistentFrameDecoder(_testVideoPath);

        // Act
        var frame1 = decoder.DecodeFrameAt(TimeSpan.FromSeconds(1.0));
        var frame2 = decoder.DecodeFrameAt(TimeSpan.FromSeconds(2.0));
        var frame3 = decoder.DecodeFrameAt(TimeSpan.FromSeconds(1.0)); // Same as frame1

        // Assert
        Assert.NotNull(frame1);
        Assert.NotNull(frame2);
        Assert.NotNull(frame3);

        // Different timestamps should have different data
        Assert.NotEqual(frame1.TimePosition, frame2.TimePosition);

        // Same timestamp should return same time (within tolerance)
        Assert.Equal(frame1.TimePosition, frame3.TimePosition);
    }

    [Fact]
    public void DecodeFrameAt_WithNegativeTime_ThrowsArgumentException()
    {
        // Arrange
        using var decoder = new PersistentFrameDecoder(_testVideoPath);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            decoder.DecodeFrameAt(TimeSpan.FromSeconds(-1.0)));
    }
}
