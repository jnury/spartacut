using Bref.Models;
using Bref.ViewModels;
using Xunit;

namespace Bref.Tests.ViewModels;

public class TimelineViewModelTests
{
    [Fact]
    public void SeekToPixel_UpdatesCurrentTime()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            TimelineHeight = 200,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(500); // Middle of timeline

        // Assert
        Assert.Equal(50, viewModel.CurrentTime.TotalSeconds);
    }

    [Fact]
    public void SeekToPixel_ClampsToZero()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(-100); // Negative position

        // Assert
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentTime);
    }

    [Fact]
    public void SeekToPixel_ClampsToDuration()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(2000); // Beyond timeline

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(100), viewModel.CurrentTime);
    }

    [Fact]
    public void PlayheadPosition_ReflectsCurrentTime()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            },
            CurrentTime = TimeSpan.FromSeconds(25)
        };

        // Act
        var position = viewModel.PlayheadPosition;

        // Assert
        Assert.Equal(250, position); // 25 seconds * 10 px/s = 250 pixels
    }
}
