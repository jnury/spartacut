using SpartaCut.Core.Models;
using SpartaCut.Core.ViewModels;
using Xunit;

namespace SpartaCut.Tests.ViewModels;

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

    [Fact]
    public void StartSelection_UpdatesSelectionProperties()
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
        viewModel.StartSelection(200); // 20 seconds

        // Assert
        Assert.True(viewModel.Selection.IsActive);
        Assert.Equal(20, viewModel.Selection.SelectionStart.TotalSeconds);
        Assert.Equal(200, viewModel.SelectionStartPixel);
    }

    [Fact]
    public void UpdateSelection_UpdatesWidth()
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
        viewModel.StartSelection(200); // 20 seconds
        viewModel.UpdateSelection(500); // 50 seconds

        // Assert
        Assert.Equal(50, viewModel.Selection.SelectionEnd.TotalSeconds);
        Assert.Equal(500, viewModel.SelectionEndPixel);
        Assert.Equal(300, viewModel.SelectionWidth); // 500 - 200 = 300 pixels
    }

    [Fact]
    public void ClearSelection_ResetsProperties()
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
        viewModel.StartSelection(200);
        viewModel.UpdateSelection(500);

        // Act
        viewModel.ClearSelection();

        // Assert
        Assert.False(viewModel.Selection.IsActive);
        Assert.Equal(0, viewModel.SelectionStartPixel);
        Assert.Equal(0, viewModel.SelectionEndPixel);
        Assert.Equal(0, viewModel.SelectionWidth);
    }

    [Fact]
    public void SelectionNormalizedStartPixel_AlwaysLeftmost()
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

        // Test forward drag
        viewModel.StartSelection(200); // 20 seconds
        viewModel.UpdateSelection(500); // 50 seconds
        Assert.Equal(200, viewModel.SelectionNormalizedStartPixel);

        // Test backward drag
        viewModel.StartSelection(500); // 50 seconds
        viewModel.UpdateSelection(200); // 20 seconds
        Assert.Equal(200, viewModel.SelectionNormalizedStartPixel);
    }
}
