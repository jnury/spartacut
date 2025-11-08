using System;
using Bref.Models;
using Bref.ViewModels;
using Xunit;

namespace Bref.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void InitializeVideo_SetsUpSegmentManager()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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

        // Act
        viewModel.InitializeVideo(metadata);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(10), viewModel.VirtualDuration);
        Assert.Equal(1, viewModel.SegmentCount);
        Assert.NotNull(viewModel.Timeline.VideoMetadata);
        Assert.Equal(metadata, viewModel.Timeline.VideoMetadata);
    }

    [Fact]
    public void DeleteSelection_CallsSegmentManager()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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
        viewModel.InitializeVideo(metadata);

        // Create a selection (delete 2-3 minute mark)
        viewModel.Timeline.Selection.StartSelection(TimeSpan.FromMinutes(2));
        viewModel.Timeline.Selection.UpdateSelection(TimeSpan.FromMinutes(3));

        var durationBefore = viewModel.VirtualDuration;

        // Act
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert
        // Duration should be 9 minutes (10 - 1 minute deleted)
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.True(viewModel.VirtualDuration < durationBefore);
    }

    [Fact]
    public void DeleteSelection_ClearsSelection()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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
        viewModel.InitializeVideo(metadata);

        // Create a selection
        viewModel.Timeline.Selection.StartSelection(TimeSpan.FromMinutes(2));
        viewModel.Timeline.Selection.UpdateSelection(TimeSpan.FromMinutes(3));

        Assert.True(viewModel.Timeline.Selection.IsActive);

        // Act
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert
        Assert.False(viewModel.Timeline.Selection.IsActive);
    }

    [Fact]
    public void DeleteSelection_UpdatesVirtualDuration()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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
        viewModel.InitializeVideo(metadata);

        // Create a selection (delete first minute)
        viewModel.Timeline.Selection.StartSelection(TimeSpan.FromSeconds(0));
        viewModel.Timeline.Selection.UpdateSelection(TimeSpan.FromMinutes(1));

        // Act
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
    }

    [Fact]
    public void Undo_RestoresState()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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
        viewModel.InitializeVideo(metadata);

        // Delete a segment
        viewModel.Timeline.Selection.StartSelection(TimeSpan.FromMinutes(2));
        viewModel.Timeline.Selection.UpdateSelection(TimeSpan.FromMinutes(3));
        viewModel.DeleteSelectionCommand.Execute(null);

        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.True(viewModel.CanUndo);

        // Act
        viewModel.UndoCommand.Execute(null);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(10), viewModel.VirtualDuration);
        Assert.Equal(1, viewModel.SegmentCount);
    }

    [Fact]
    public void CanDelete_FalseWhenNoSelection()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
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
        viewModel.InitializeVideo(metadata);

        // Act & Assert - no selection
        Assert.False(viewModel.CanDelete);

        // Create selection
        viewModel.Timeline.Selection.StartSelection(TimeSpan.FromMinutes(2));
        viewModel.Timeline.Selection.UpdateSelection(TimeSpan.FromMinutes(3));

        // Assert - with valid selection
        Assert.True(viewModel.CanDelete);
    }

    [Fact]
    public void VlcPlaybackEngine_PropertyExists()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Assert - VlcPlaybackEngine property should exist
        Assert.NotNull(viewModel.VlcPlaybackEngine);
    }

    [Fact]
    public void PlayCommand_ExistsAndIsDisabledInitially()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Assert - Play command should exist but be disabled (no video loaded)
        Assert.NotNull(viewModel.PlayCommand);
        Assert.False(viewModel.CanPlay);
        Assert.False(viewModel.PlayCommand.CanExecute(null));
    }

    [Fact]
    public void PauseCommand_ExistsAndIsDisabledInitially()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Assert - Pause command should exist but be disabled (not playing)
        Assert.NotNull(viewModel.PauseCommand);
        Assert.False(viewModel.CanPause);
        Assert.False(viewModel.PauseCommand.CanExecute(null));
    }

    [Fact]
    public void StopCommand_Exists()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Assert - Stop command should exist
        Assert.NotNull(viewModel.StopCommand);
        Assert.True(viewModel.StopCommand.CanExecute(null)); // Stop can always execute
    }

    [Fact]
    public void IsPlaying_InitiallyFalse()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Assert - IsPlaying should be false initially
        Assert.False(viewModel.IsPlaying);
    }
}
