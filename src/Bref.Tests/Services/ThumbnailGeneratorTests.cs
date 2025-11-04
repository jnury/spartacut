using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class ThumbnailGeneratorTests
{
    [Fact]
    public void Generate_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var generator = new ThumbnailGenerator();
        var invalidPath = "/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            generator.Generate(invalidPath, TimeSpan.FromSeconds(5), 160, 90));
    }
}
