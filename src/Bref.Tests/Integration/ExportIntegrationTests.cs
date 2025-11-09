using Bref.Core.Models;
using Bref.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Bref.Tests.Integration;

/// <summary>
/// Integration tests for export functionality
/// NOTE: These tests require sample-30s.mp4 in samples/ directory
/// </summary>
public class ExportIntegrationTests
{
    private const string SampleVideo = "samples/sample-30s.mp4";

    [Fact(Skip = "Requires sample video file")]
    public async Task ExportSingleSegment_CreatesValidVideo()
    {
        // Arrange
        if (!File.Exists(SampleVideo))
        {
            throw new FileNotFoundException($"Sample video not found: {SampleVideo}");
        }

        var service = new ExportService();
        var outputPath = Path.GetTempFileName() + ".mp4";

        var segments = new SegmentList();
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        var metadata = new VideoMetadata
        {
            FilePath = SampleVideo,
            Duration = TimeSpan.FromSeconds(30),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = SampleVideo,
            OutputFilePath = outputPath,
            Segments = segments,
            Metadata = metadata,
            UseHardwareAcceleration = false // Force software for reliability
        };

        var progressReports = new System.Collections.Generic.List<ExportProgress>();
        var progress = new Progress<ExportProgress>(p => progressReports.Add(p));

        try
        {
            // Act
            var success = await service.ExportAsync(options, progress, CancellationToken.None);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));

            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0);

            // Should have progress reports
            Assert.NotEmpty(progressReports);
            Assert.Contains(progressReports, p => p.Stage == ExportStage.Complete);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact(Skip = "Requires sample video file")]
    public async Task ExportMultipleSegments_ConcatenatesCorrectly()
    {
        // Arrange
        if (!File.Exists(SampleVideo))
        {
            throw new FileNotFoundException($"Sample video not found: {SampleVideo}");
        }

        var service = new ExportService();
        var outputPath = Path.GetTempFileName() + ".mp4";

        var segments = new SegmentList();
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromSeconds(30)
        });

        // Delete middle 10 seconds (10-20)
        segments.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        var metadata = new VideoMetadata
        {
            FilePath = SampleVideo,
            Duration = TimeSpan.FromSeconds(30),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = SampleVideo,
            OutputFilePath = outputPath,
            Segments = segments,
            Metadata = metadata,
            UseHardwareAcceleration = false
        };

        var progress = new Progress<ExportProgress>();

        try
        {
            // Act
            var success = await service.ExportAsync(options, progress, CancellationToken.None);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));

            // Output should be ~20 seconds (30 - 10 deleted)
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
