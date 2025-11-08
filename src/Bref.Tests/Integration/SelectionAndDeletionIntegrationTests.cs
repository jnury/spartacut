using System;
using Bref.Core.Models;
using Bref.Core.ViewModels;
using Bref.Tests.Mocks;
using Xunit;

namespace Bref.Tests.Integration;

/// <summary>
/// Integration tests for Week 6: Selection & Deletion UI
/// Tests full workflow from user interaction to data layer
/// </summary>
public class SelectionAndDeletionIntegrationTests
{
    [Fact]
    public void FullDeletionWorkflow_LoadVideoSelectDeleteVerify()
    {
        // Arrange - Initialize MainWindowViewModel with video
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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

        // Set timeline dimensions
        viewModel.Timeline.TimelineWidth = 1000;
        viewModel.Timeline.TimelineHeight = 150;

        // Act - User selects segment (2-3 minute mark) and deletes
        viewModel.Timeline.StartSelectionCommand.Execute(200.0); // 2 minutes at 10px/s
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0); // 3 minutes

        Assert.True(viewModel.Timeline.Selection.IsValid);
        Assert.True(viewModel.CanDelete);

        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - Timeline contracts, duration reduced, selection cleared
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.Equal(2, viewModel.SegmentCount); // 0-2min and 3-10min
        Assert.False(viewModel.Timeline.Selection.IsActive);
        Assert.False(viewModel.CanDelete);
    }

    [Fact]
    public void MultipleDeletions_VirtualTimelineCalculationsCorrect()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Act - Delete multiple segments
        // Delete 1: Remove 2-3 minute mark (1 minute deleted, 9 minutes remain)
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);

        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.Equal(2, viewModel.SegmentCount);

        // Delete 2: Remove exactly 1 minute from the virtual timeline
        // After first deletion, timeline is contracted: 9min across 1000px = 1.852 px/s
        // To delete 1 minute = 60 seconds, we need 111.11 pixels
        // Delete from virtual 1min (111.11px) to virtual 2min (222.22px)
        var metrics = viewModel.Timeline.Metrics!;
        var pixel1min = metrics.TimeToPixel(TimeSpan.FromMinutes(1));
        var pixel2min = metrics.TimeToPixel(TimeSpan.FromMinutes(2));

        viewModel.Timeline.StartSelectionCommand.Execute(pixel1min);
        viewModel.Timeline.UpdateSelectionCommand.Execute(pixel2min);
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - Should have 8 minutes remaining
        // Segments: [0-1min], [3-10min] = 2 segments
        // (Deleting 1-2min splits first segment [0-2min] into [0-1min] only)
        Assert.Equal(TimeSpan.FromMinutes(8), viewModel.VirtualDuration);
        Assert.Equal(2, viewModel.SegmentCount);
    }

    [Fact]
    public void UndoRedoAfterDeletion_RestoresAndReapplies()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Act - Delete segment
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);

        var durationAfterDelete = viewModel.VirtualDuration;
        Assert.Equal(TimeSpan.FromMinutes(9), durationAfterDelete);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);

        // Act - Undo
        viewModel.UndoCommand.Execute(null);

        // Assert - Restored to original state
        Assert.Equal(TimeSpan.FromMinutes(10), viewModel.VirtualDuration);
        Assert.Equal(1, viewModel.SegmentCount);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);

        // Act - Redo
        viewModel.RedoCommand.Execute(null);

        // Assert - Deletion reapplied
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.Equal(2, viewModel.SegmentCount);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void SelectionAtVideoStart_DeletesCorrectly()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Act - Delete first minute
        viewModel.Timeline.StartSelectionCommand.Execute(0.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(100.0); // 1 minute
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - 9 minutes remain, starts at source time 1 minute
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.Equal(1, viewModel.SegmentCount);

        // Verify virtual to source time conversion
        var sourceTimeAtStart = viewModel.Timeline.VirtualToSourceTime(TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMinutes(1), sourceTimeAtStart);
    }

    [Fact]
    public void SelectionAtVideoEnd_DeletesCorrectly()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Act - Delete last minute
        viewModel.Timeline.StartSelectionCommand.Execute(900.0); // 9 minutes
        viewModel.Timeline.UpdateSelectionCommand.Execute(1000.0); // 10 minutes
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - 9 minutes remain, ends at source time 9 minutes
        Assert.Equal(TimeSpan.FromMinutes(9), viewModel.VirtualDuration);
        Assert.Equal(1, viewModel.SegmentCount);

        // Verify virtual to source time conversion at end
        var sourceTimeAtEnd = viewModel.Timeline.VirtualToSourceTime(TimeSpan.FromMinutes(9));
        Assert.Equal(TimeSpan.FromMinutes(9), sourceTimeAtEnd);
    }

    [Fact]
    public void TimelineMetrics_UpdateAfterDeletion()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Assert initial metrics
        var initialMetrics = viewModel.Timeline.Metrics;
        Assert.NotNull(initialMetrics);
        Assert.Equal(TimeSpan.FromMinutes(10), initialMetrics.TotalDuration);
        Assert.Equal(1000, initialMetrics.TimelineWidth);

        // Act - Delete segment
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - Metrics updated to virtual duration
        var updatedMetrics = viewModel.Timeline.Metrics;
        Assert.NotNull(updatedMetrics);
        Assert.Equal(TimeSpan.FromMinutes(9), updatedMetrics.TotalDuration);
        Assert.Equal(1000, updatedMetrics.TimelineWidth);

        // Pixels per second should change (timeline contracts)
        var pixelsPerSecond = updatedMetrics.TimelineWidth / updatedMetrics.TotalDuration.TotalSeconds;
        Assert.Equal(1000.0 / (9 * 60), pixelsPerSecond);
    }

    [Fact]
    public void PlayheadPosition_UpdatesCorrectlyAfterDeletion()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Set playhead to 5 minutes (middle of video)
        viewModel.Timeline.CurrentTime = TimeSpan.FromMinutes(5);

        // Act - Delete 2-3 minute mark (before playhead)
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - Playhead at source time 5min should map to virtual time 4min
        // Virtual timeline: [0-2min (source 0-2), 2-9min (source 3-10)]
        // Source 5min is in second segment, virtual time = 2 + (5-3) = 4min
        var virtualTime = viewModel.Timeline.SourceToVirtualTime(TimeSpan.FromMinutes(5));
        Assert.NotNull(virtualTime);
        Assert.Equal(TimeSpan.FromMinutes(4), virtualTime.Value);

        // Playhead position should reflect virtual time
        var expectedPixel = (4.0 / 9.0) * 1000; // 4min / 9min total * 1000px
        Assert.Equal(expectedPixel, viewModel.Timeline.PlayheadPosition, precision: 1);
    }

    [Fact]
    public void DeletedRegions_ReflectInTimeline()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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

        // Act - Delete middle segment
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);

        // Assert - GetDeletedRegions should return the deleted segment
        var deletedRegions = viewModel.Timeline.GetDeletedRegions();
        Assert.Single(deletedRegions);
        Assert.Equal(TimeSpan.FromMinutes(2), deletedRegions[0].Start);
        Assert.Equal(TimeSpan.FromMinutes(3), deletedRegions[0].End);
    }

    [Fact]
    public void MultipleUndoRedo_HandlesComplexHistory()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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
        viewModel.Timeline.TimelineWidth = 1000;

        // Act - Delete three segments, each exactly 1 minute
        // Delete 1: 2-3 min in source
        viewModel.Timeline.StartSelectionCommand.Execute(200.0);
        viewModel.Timeline.UpdateSelectionCommand.Execute(300.0);
        viewModel.DeleteSelectionCommand.Execute(null);
        var duration1 = viewModel.VirtualDuration;

        // Delete 2: 1 minute from virtual timeline (uses contracted coordinates)
        var metrics2 = viewModel.Timeline.Metrics!;
        var pixel4min = metrics2.TimeToPixel(TimeSpan.FromMinutes(4));
        var pixel5min = metrics2.TimeToPixel(TimeSpan.FromMinutes(5));
        viewModel.Timeline.StartSelectionCommand.Execute(pixel4min);
        viewModel.Timeline.UpdateSelectionCommand.Execute(pixel5min);
        viewModel.DeleteSelectionCommand.Execute(null);
        var duration2 = viewModel.VirtualDuration;

        // Delete 3: 1 minute from virtual timeline (uses contracted coordinates)
        var metrics3 = viewModel.Timeline.Metrics!;
        var pixel6min = metrics3.TimeToPixel(TimeSpan.FromMinutes(6));
        var pixel7min = metrics3.TimeToPixel(TimeSpan.FromMinutes(7));
        viewModel.Timeline.StartSelectionCommand.Execute(pixel6min);
        viewModel.Timeline.UpdateSelectionCommand.Execute(pixel7min);
        viewModel.DeleteSelectionCommand.Execute(null);
        var duration3 = viewModel.VirtualDuration;

        Assert.Equal(TimeSpan.FromMinutes(9), duration1);
        Assert.Equal(TimeSpan.FromMinutes(8), duration2);
        Assert.Equal(TimeSpan.FromMinutes(7), duration3);

        // Undo all three
        viewModel.UndoCommand.Execute(null);
        Assert.Equal(duration2, viewModel.VirtualDuration);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(duration1, viewModel.VirtualDuration);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(TimeSpan.FromMinutes(10), viewModel.VirtualDuration);
        Assert.False(viewModel.CanUndo);

        // Redo all three
        viewModel.RedoCommand.Execute(null);
        Assert.Equal(duration1, viewModel.VirtualDuration);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal(duration2, viewModel.VirtualDuration);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal(duration3, viewModel.VirtualDuration);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void VolumeControl_SetMultipleTimes_MaintainsCorrectValue()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
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

        // Act - Set volume multiple times
        viewModel.Volume = 0.5;
        Assert.Equal(0.5, viewModel.Volume);
        Assert.Equal(0.5f, viewModel.PlaybackEngine.Volume);

        viewModel.Volume = 0.75;
        Assert.Equal(0.75, viewModel.Volume);
        Assert.Equal(0.75f, viewModel.PlaybackEngine.Volume);

        viewModel.Volume = 0.0;
        Assert.Equal(0.0, viewModel.Volume);
        Assert.Equal(0.0f, viewModel.PlaybackEngine.Volume);

        // Assert - Final volume is correct
        Assert.Equal(0.0, viewModel.Volume);
    }

    [Fact]
    public void ToggleMute_PreservesVolumeLevel()
    {
        // Arrange
        var viewModel = new MainWindowViewModel(new MockPlaybackEngine());
        viewModel.Volume = 0.8;

        // Act - Mute
        viewModel.ToggleMute();
        Assert.Equal(0.0, viewModel.Volume);

        // Act - Unmute
        viewModel.ToggleMute();

        // Assert - Volume restored
        Assert.Equal(0.8, viewModel.Volume);
    }
}
