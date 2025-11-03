using Bref.Models;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class VideoServiceTests
{
    [Fact]
    public async Task LoadVideo_WithInvalidExtension_ThrowsNotSupportedException()
    {
        // Arrange
        var service = new VideoService();
        var invalidPath = "/tmp/test-video.avi";

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.LoadVideoAsync(invalidPath, new Progress<LoadProgress>(_ => { })));
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
}
