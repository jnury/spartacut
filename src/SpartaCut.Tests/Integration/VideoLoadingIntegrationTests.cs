using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using Xunit;

namespace SpartaCut.Tests.Integration;

/// <summary>
/// Integration tests for video loading workflow.
/// Requires test MP4 file to be present.
/// </summary>
public class VideoLoadingIntegrationTests
{
    private readonly string _testVideoPath;

    public VideoLoadingIntegrationTests()
    {
        // Look for test video in user home directory
        _testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4"
        );
    }

    [Fact]
    public async Task VideoService_LoadValidMp4_ReturnsCompleteMetadata()
    {
        // Skip if no test video
        if (!File.Exists(_testVideoPath))
        {
            // Test skipped - no test video available
            return;
        }

        // Arrange
        var service = new VideoService();
        var progressReports = new List<LoadProgress>();
        var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

        // Act
        var metadata = await service.LoadVideoAsync(_testVideoPath, progress);

        // Assert - Metadata
        Assert.NotNull(metadata);
        Assert.Equal(_testVideoPath, metadata.FilePath);
        Assert.True(metadata.Duration > TimeSpan.Zero);
        Assert.True(metadata.Width > 0);
        Assert.True(metadata.Height > 0);
        Assert.True(metadata.FrameRate > 0);
        Assert.NotEmpty(metadata.CodecName);

        // Assert - Waveform
        Assert.NotNull(metadata.Waveform);
        Assert.NotEmpty(metadata.Waveform.Peaks);
        Assert.Equal(metadata.Duration, metadata.Waveform.Duration);

        // Assert - Progress Reporting
        Assert.Contains(progressReports, p => p.Stage == LoadStage.Validating);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.ExtractingMetadata);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.GeneratingWaveform);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.Complete);

        // Progress should increase monotonically (mostly)
        var lastPercentage = -1;
        foreach (var report in progressReports.Where(p => p.Stage != LoadStage.Complete))
        {
            Assert.True(report.Percentage >= lastPercentage || report.Stage != progressReports[progressReports.IndexOf(report) - 1].Stage);
            lastPercentage = report.Percentage;
        }
    }

    [Fact]
    public async Task VideoService_CancellationToken_CancelsOperation()
    {
        // Skip if no test video
        if (!File.Exists(_testVideoPath))
        {
            return;
        }

        // Arrange
        var service = new VideoService();
        var cts = new CancellationTokenSource();
        var progress = new Progress<LoadProgress>(p =>
        {
            // Cancel after first progress report
            if (p.Percentage > 0)
            {
                cts.Cancel();
            }
        });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.LoadVideoAsync(_testVideoPath, progress, cts.Token));
    }
}
