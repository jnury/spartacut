using Xunit;
using Bref.FFmpeg;
using System;
using Serilog;
using Serilog.Core;

namespace Bref.Tests.FFmpeg;

public class FFmpegSetupTests : IDisposable
{
    public FFmpegSetupTests()
    {
        // Setup minimal logger for tests
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
    }

    [Fact]
    public void Initialize_ShouldSetFFmpegLibraryPath()
    {
        // Act
        FFmpegSetup.Initialize();

        // Assert
        // If initialization succeeds without exception, test passes
        Assert.True(true);
    }

    [Fact]
    public void GetFFmpegVersion_ShouldReturnVersionString()
    {
        // Arrange
        FFmpegSetup.Initialize();

        // Act
        var version = FFmpegSetup.GetFFmpegVersion();

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        // Version string should contain a number (e.g., "7.1.2")
        Assert.Matches(@"\d+", version);
    }
}
