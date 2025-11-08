using Bref.Core.Models;
using Bref.Core.Services;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bref.Tests.Services;

public class VideoServiceTests
{
    [Fact]
    public async Task LoadVideo_WithInvalidExtension_ThrowsNotSupportedException()
    {
        // Arrange
        var service = new VideoService();

        // Create a temporary .avi file (empty is fine, we're testing extension validation)
        var invalidPath = Path.Combine(Path.GetTempPath(), $"test-video-{Guid.NewGuid()}.avi");
        File.WriteAllText(invalidPath, "dummy content");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                service.LoadVideoAsync(invalidPath, new Progress<LoadProgress>(_ => { })));
        }
        finally
        {
            // Cleanup
            if (File.Exists(invalidPath))
                File.Delete(invalidPath);
        }
    }

    [Fact]
    public async Task LoadVideo_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var service = new VideoService();
        var missingPath = "/nonexistent/video.mp4";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.LoadVideoAsync(missingPath, new Progress<LoadProgress>(_ => { })));
    }

    [Fact]
    public async Task LoadVideo_WithValidMp4_ReturnsMetadata()
    {
        // Arrange
        var service = new VideoService();
        // Note: This test requires a real MP4 file for integration testing
        // Skip if file doesn't exist
        var testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4"
        );

        if (!File.Exists(testVideoPath))
        {
            // Skip test if no test video available
            return;
        }

        var progressReports = new List<LoadProgress>();
        var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

        // Act
        var metadata = await service.LoadVideoAsync(testVideoPath, progress);

        // Assert
        Assert.NotNull(metadata);
        Assert.True(metadata.Duration > TimeSpan.Zero);
        Assert.True(metadata.Width > 0);
        Assert.True(metadata.Height > 0);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.ExtractingMetadata);
    }
}
