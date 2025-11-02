using Xunit;
using Bref.FFmpeg;
using Bref.Models;
using System;
using System.IO;
using Serilog;

namespace Bref.Tests.FFmpeg;

public class FrameExtractorTests : IDisposable
{
    private const string TestVideoPath = "/path/to/test/video.mp4"; // TODO: Update with real test video

    public FrameExtractorTests()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        FFmpegSetup.Initialize();
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
    }

    [Fact(Skip = "Requires test video file")]
    public void ExtractMetadata_WithValidMP4_ReturnsMetadata()
    {
        // Arrange
        var extractor = new FrameExtractor();

        // Act
        var metadata = extractor.ExtractMetadata(TestVideoPath);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.Width > 0);
        Assert.True(metadata.Height > 0);
        Assert.True(metadata.Duration > TimeSpan.Zero);
        Assert.Equal("h264", metadata.CodecName.ToLower());
    }

    [Fact]
    public void ExtractMetadata_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var extractor = new FrameExtractor();
        var nonExistentPath = "/path/to/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => extractor.ExtractMetadata(nonExistentPath));
    }
}
