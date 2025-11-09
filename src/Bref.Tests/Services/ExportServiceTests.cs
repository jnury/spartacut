using Bref.Core.Services;
using Xunit;

namespace Bref.Tests.Services;

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
}
