using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SpartaCut.Tests.Services;

public class ExportServiceEdgeCaseTests
{
    [Fact]
    public async Task Export_WithMissingSourceFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromMinutes(1)
        });

        var metadata = new VideoMetadata
        {
            FilePath = "nonexistent.mp4",
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = "nonexistent.mp4",
            OutputFilePath = "output.mp4",
            Segments = segments,
            Metadata = metadata
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await service.ExportAsync(options, new Progress<ExportProgress>());
        });
    }

    [Fact]
    public async Task Export_WithNoSegments_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromMinutes(1)
        });

        // Delete all segments
        segments.DeleteSegment(TimeSpan.Zero, TimeSpan.FromMinutes(1));

        // Create temporary file so source file exists
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "dummy");

        var metadata = new VideoMetadata
        {
            FilePath = tempFile,
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = tempFile,
            OutputFilePath = "output.mp4",
            Segments = segments,
            Metadata = metadata
        };

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await service.ExportAsync(options, new Progress<ExportProgress>());
            });
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task Export_WithEmptyOutputPath_ThrowsArgumentException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromMinutes(1)
        });

        // Create temporary file so source file exists
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "dummy");

        var metadata = new VideoMetadata
        {
            FilePath = tempFile,
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = tempFile,
            OutputFilePath = "",
            Segments = segments,
            Metadata = metadata
        };

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await service.ExportAsync(options, new Progress<ExportProgress>());
            });
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
