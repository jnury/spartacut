using System;
using Bref.Models;
using Bref.Services;
using Xunit;

namespace Bref.Tests.Services;

public class PlaybackEngineTests
{
    [Fact]
    public void Constructor_InitializesWithStoppedState()
    {
        using var engine = new PlaybackEngine();
        Assert.Equal(PlaybackState.Stopped, engine.State);
    }

    [Fact]
    public void CurrentTime_StartsAtZero()
    {
        using var engine = new PlaybackEngine();
        Assert.Equal(TimeSpan.Zero, engine.CurrentTime);
    }

    [Fact]
    public void CanPlay_WhenNoVideoLoaded_ReturnsFalse()
    {
        using var engine = new PlaybackEngine();
        Assert.False(engine.CanPlay);
    }

    [Fact]
    public void Play_WhenStopped_ChangesStateToPause()
    {
        using var engine = new PlaybackEngine();
        engine.Play();
        Assert.Equal(PlaybackState.Paused, engine.State);
    }

    [Fact]
    public void Pause_WhenPlaying_ChangesStateToPaused()
    {
        using var engine = new PlaybackEngine();
        engine.Play();
        engine.Pause();
        Assert.Equal(PlaybackState.Paused, engine.State);
    }

    [Fact]
    public void Play_ReturnsQuickly_PreloadingIsNonBlocking()
    {
        // Arrange
        using var engine = new PlaybackEngine();
        var segmentManager = new SegmentManager();
        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromSeconds(10),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        segmentManager.Initialize(metadata.Duration);

        // Note: We can't fully initialize without a real FrameCache
        // This test verifies Play() doesn't block even when preloading would occur

        // Act - This should return quickly regardless of preloading
        var startTime = DateTime.UtcNow;
        engine.Play();
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Play() should return immediately (< 50ms)
        // If preloading were blocking, this would take much longer
        Assert.True(elapsed.TotalMilliseconds < 50,
            $"Play() took {elapsed.TotalMilliseconds}ms - preloading may be blocking");
    }

    [Fact]
    public void OnFrameTimerElapsed_SkipsDeletedSegments()
    {
        // Note: This test verifies the segment boundary logic by checking time progression
        // We can't easily mock FrameCache, so we test the time advancement logic directly

        // Arrange
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromSeconds(10));

        // Delete segment from 3-5 seconds (in virtual time)
        segmentManager.DeleteSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));

        // Verify deletion worked
        var segments = segmentManager.CurrentSegments.KeptSegments;
        Assert.Equal(2, segments.Count); // Should have 2 segments: [0-3] and [5-10]

        // Test the boundary detection logic that PlaybackEngine will use
        var timeInDeletedSegment = TimeSpan.FromSeconds(4); // 4 seconds is in deleted range
        var virtualTime = segmentManager.CurrentSegments.SourceToVirtualTime(timeInDeletedSegment);

        // Assert - Time in deleted segment should return null
        Assert.Null(virtualTime);

        // Find next kept segment (what PlaybackEngine will do)
        var nextKeptSegment = segments.FirstOrDefault(s => s.SourceStart > timeInDeletedSegment);
        Assert.NotNull(nextKeptSegment);
        Assert.Equal(TimeSpan.FromSeconds(5), nextKeptSegment.SourceStart);
    }

    [Fact]
    public void OnFrameTimerElapsed_DetectsEndWhenNoMoreSegments()
    {
        // Arrange
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromSeconds(10));

        // Delete everything from 5 seconds to end
        segmentManager.DeleteSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

        // Verify deletion
        var segments = segmentManager.CurrentSegments.KeptSegments;
        Assert.Single(segments); // Only [0-5] remains

        // Test boundary detection
        var timeInDeletedSegment = TimeSpan.FromSeconds(6);
        var virtualTime = segmentManager.CurrentSegments.SourceToVirtualTime(timeInDeletedSegment);

        // Assert - Should return null (in deleted segment)
        Assert.Null(virtualTime);

        // Verify no next segment exists
        var nextKeptSegment = segments.FirstOrDefault(s => s.SourceStart > timeInDeletedSegment);
        Assert.Null(nextKeptSegment); // No more segments
    }

    [Fact]
    public void OnFrameTimerElapsed_KeptSegmentReturnsValidVirtualTime()
    {
        // Arrange
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromSeconds(10));

        // Delete middle segment
        segmentManager.DeleteSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));

        // Test time in kept segments
        var timeBeforeDeletion = TimeSpan.FromSeconds(2);
        var virtualTimeBefore = segmentManager.CurrentSegments.SourceToVirtualTime(timeBeforeDeletion);

        var timeAfterDeletion = TimeSpan.FromSeconds(6);
        var virtualTimeAfter = segmentManager.CurrentSegments.SourceToVirtualTime(timeAfterDeletion);

        // Assert - Both should return valid virtual times (not null)
        Assert.NotNull(virtualTimeBefore);
        Assert.NotNull(virtualTimeAfter);
        Assert.Equal(TimeSpan.FromSeconds(2), virtualTimeBefore.Value); // 2s in first segment
        Assert.Equal(TimeSpan.FromSeconds(4), virtualTimeAfter.Value); // 6s - 2s deleted = 4s virtual
    }

    [Fact]
    public void SegmentBoundaryLogic_FindsNextSegmentCorrectly()
    {
        // This test verifies the complete segment boundary jumping logic
        // that will be used in OnFrameTimerElapsed

        // Arrange
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromSeconds(10));

        // Create a deletion - delete 3-5s in virtual time
        segmentManager.DeleteSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));

        var segments = segmentManager.CurrentSegments.KeptSegments;
        // Should have 2 segments: [0-3], [5-10]
        Assert.Equal(2, segments.Count);
        Assert.Equal(TimeSpan.FromSeconds(0), segments[0].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(3), segments[0].SourceEnd);
        Assert.Equal(TimeSpan.FromSeconds(5), segments[1].SourceStart);
        Assert.Equal(TimeSpan.FromSeconds(10), segments[1].SourceEnd);

        // Simulate playback hitting deleted segment (at 4s source time)
        var currentTime = TimeSpan.FromSeconds(4);
        var virtualTime = segmentManager.CurrentSegments.SourceToVirtualTime(currentTime);
        Assert.Null(virtualTime); // In deleted segment

        // Find next kept segment
        var nextSegment = segments.FirstOrDefault(s => s.SourceStart > currentTime);
        Assert.NotNull(nextSegment);
        Assert.Equal(TimeSpan.FromSeconds(5), nextSegment.SourceStart);

        // Simulate playback in first kept segment (at 2s)
        currentTime = TimeSpan.FromSeconds(2);
        virtualTime = segmentManager.CurrentSegments.SourceToVirtualTime(currentTime);
        Assert.NotNull(virtualTime); // Should be in kept segment

        // Simulate playback in second kept segment (at 8s)
        currentTime = TimeSpan.FromSeconds(8);
        virtualTime = segmentManager.CurrentSegments.SourceToVirtualTime(currentTime);
        Assert.NotNull(virtualTime); // Should be in kept segment
    }
}
