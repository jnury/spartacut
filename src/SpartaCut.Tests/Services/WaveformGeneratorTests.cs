using System;
using System.IO;
using SpartaCut.Core.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class WaveformGeneratorTests
{
    [Fact]
    public void GenerateWaveform_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var generator = new WaveformGenerator();
        var invalidPath = "/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            generator.Generate(invalidPath, new Progress<int>(_ => { })));
    }
}
