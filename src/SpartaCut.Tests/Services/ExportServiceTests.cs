using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class ExportServiceTests
{
    [Fact]
    public async Task DetectHardwareEncoders_ReturnsArray()
    {
        // Arrange
        var service = new ExportService();

        // Act
        var encoders = await service.DetectHardwareEncodersAsync();

        // Assert
        Assert.NotNull(encoders);
        // May be empty on systems without hardware encoding
    }

    [Fact]
    public async Task GetRecommendedEncoder_ReturnsEncoderOrNull()
    {
        // Arrange
        var service = new ExportService();

        // Act
        var encoder = await service.GetRecommendedEncoderAsync();

        // Assert
        // encoder is either a hardware encoder name or null (software)
        if (encoder != null)
        {
            Assert.Contains(encoder, new[] { "h264_nvenc", "h264_qsv", "h264_amf" });
        }
    }

    [Fact]
    public void BuildFilterComplex_SingleSegment_CreatesSimpleTrim()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();

        // Add single segment (entire video)
        segments.KeptSegments.Add(new VideoSegment
        {
            SourceStart = TimeSpan.Zero,
            SourceEnd = TimeSpan.FromMinutes(10)
        });

        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(10),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        // Use reflection to call private method for testing
        var method = typeof(ExportService).GetMethod("BuildFilterComplex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var filter = (string)method!.Invoke(service, new object[] { segments, metadata })!;

        // Assert
        Assert.Contains("trim=start=0", filter);
        Assert.Contains("atrim=start=0", filter);
    }
}
