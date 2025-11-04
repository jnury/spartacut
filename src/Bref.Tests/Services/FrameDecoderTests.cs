using Bref.Services;
using Bref.Models;
using Xunit;

namespace Bref.Tests.Services;

public class FrameDecoderTests
{
    [Fact]
    public void DecodeFrame_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var decoder = new FrameDecoder();
        var invalidPath = "/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            decoder.DecodeFrame(invalidPath, TimeSpan.Zero));
    }

    [Fact]
    public void DecodeFrame_WithNegativeTime_ThrowsArgumentException()
    {
        // Arrange
        var decoder = new FrameDecoder();
        var testPath = "/test/video.mp4"; // Will fail on file check first

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            decoder.DecodeFrame(testPath, TimeSpan.FromSeconds(-1)));

        Assert.Contains("negative", ex.Message.ToLower());
    }
}
