using Xunit;
using SpartaCut.Core.FFmpeg;
using SpartaCut.Core.Models;
using System;
using System.IO;
using Serilog;

namespace SpartaCut.Tests.FFmpeg;

public class FrameExtractorTests : IDisposable
{
    private readonly string TestVideoPath;

    public FrameExtractorTests()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Get path to sample video
        TestVideoPath = Path.Combine(
            Path.GetDirectoryName(typeof(FrameExtractorTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "samples", "sample-30s.mp4");
        TestVideoPath = Path.GetFullPath(TestVideoPath);
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
    }

    [Fact]
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
