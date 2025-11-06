using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bref.Models;
using Bref.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bref.ViewModels;

/// <summary>
/// ViewModel for timeline control managing playhead position and timeline state.
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    [ObservableProperty]
    private VideoMetadata? _videoMetadata;

    private bool _suppressUpdates = false;

    /// <summary>
    /// Segment manager for virtual timeline calculations
    /// </summary>
    public SegmentManager? SegmentManager { get; set; }

    /// <summary>
    /// Suppress timeline updates during regeneration
    /// </summary>
    public void BeginUpdate() => _suppressUpdates = true;

    /// <summary>
    /// Resume timeline updates after regeneration
    /// </summary>
    public void EndUpdate() => _suppressUpdates = false;

    [ObservableProperty]
    private TimeSpan _currentTime;

    /// <summary>
    /// Event raised when CurrentTime changes.
    /// </summary>
    public event EventHandler<TimeSpan>? CurrentTimeChanged;

    /// <summary>
    /// Event raised when segments change (deletion/undo/redo).
    /// </summary>
    public event EventHandler? SegmentsChanged;

    partial void OnCurrentTimeChanged(TimeSpan value)
    {
        CurrentTimeChanged?.Invoke(this, value);
    }

    [ObservableProperty]
    private double _timelineWidth = 1000;

    [ObservableProperty]
    private double _timelineHeight = 200;

    [ObservableProperty]
    private ObservableCollection<ThumbnailData> _thumbnails = new();

    /// <summary>
    /// Timeline metrics for coordinate conversions.
    /// Uses VIRTUAL duration (contracted timeline after deletions).
    /// </summary>
    public TimelineMetrics? Metrics
    {
        get
        {
            if (SegmentManager != null)
            {
                return new TimelineMetrics
                {
                    TotalDuration = SegmentManager.CurrentSegments.TotalDuration,
                    TimelineWidth = TimelineWidth,
                    TimelineHeight = TimelineHeight
                };
            }
            else if (VideoMetadata != null)
            {
                return new TimelineMetrics
                {
                    TotalDuration = VideoMetadata.Duration,
                    TimelineWidth = TimelineWidth,
                    TimelineHeight = TimelineHeight
                };
            }
            return null;
        }
    }

    /// <summary>
    /// Playhead position in pixels.
    /// Converts SOURCE time (CurrentTime) to VIRTUAL time for display on contracted timeline.
    /// </summary>
    public double PlayheadPosition
    {
        get
        {
            if (Metrics == null) return 0;

            // Convert source time to virtual time for contracted timeline
            var virtualTime = SourceToVirtualTime(CurrentTime);

            // If in deleted segment, clamp to start
            var displayTime = virtualTime ?? TimeSpan.Zero;

            return Metrics.TimeToPixel(displayTime);
        }
    }

    [ObservableProperty]
    private TimelineSelection _selection = new();

    /// <summary>
    /// Selection start position in pixels.
    /// </summary>
    public double SelectionStartPixel =>
        Selection.IsActive && Metrics != null
            ? Metrics.TimeToPixel(Selection.SelectionStart)
            : 0;

    /// <summary>
    /// Selection end position in pixels.
    /// </summary>
    public double SelectionEndPixel =>
        Selection.IsActive && Metrics != null
            ? Metrics.TimeToPixel(Selection.SelectionEnd)
            : 0;

    /// <summary>
    /// Selection width in pixels (always positive).
    /// </summary>
    public double SelectionWidth => Math.Abs(SelectionEndPixel - SelectionStartPixel);

    /// <summary>
    /// Selection normalized start in pixels (always leftmost).
    /// </summary>
    public double SelectionNormalizedStartPixel =>
        Math.Min(SelectionStartPixel, SelectionEndPixel);

    /// <summary>
    /// Whether a valid selection exists (for Delete command binding).
    /// </summary>
    public bool CanDeleteSelection => Selection.IsValid;

    /// <summary>
    /// Convert virtual time to source time for frame lookup
    /// </summary>
    public TimeSpan VirtualToSourceTime(TimeSpan virtualTime)
    {
        return SegmentManager?.CurrentSegments.VirtualToSourceTime(virtualTime) ?? virtualTime;
    }

    /// <summary>
    /// Convert source time to virtual time for UI display
    /// </summary>
    public TimeSpan? SourceToVirtualTime(TimeSpan sourceTime)
    {
        return SegmentManager?.CurrentSegments.SourceToVirtualTime(sourceTime) ?? sourceTime;
    }

    /// <summary>
    /// Get deleted regions for timeline rendering (gaps between kept segments)
    /// Returns list of (SourceStart, SourceEnd) tuples representing deleted portions
    /// </summary>
    public List<(TimeSpan Start, TimeSpan End)> GetDeletedRegions()
    {
        var deletedRegions = new List<(TimeSpan, TimeSpan)>();

        if (VideoMetadata == null || SegmentManager == null)
            return deletedRegions;

        var keptSegments = SegmentManager.CurrentSegments.KeptSegments;
        if (keptSegments.Count == 0)
            return deletedRegions;

        var videoDuration = VideoMetadata.Duration;

        // Check for deletion at the start
        if (keptSegments[0].SourceStart > TimeSpan.Zero)
        {
            deletedRegions.Add((TimeSpan.Zero, keptSegments[0].SourceStart));
        }

        // Check for gaps between kept segments
        for (int i = 0; i < keptSegments.Count - 1; i++)
        {
            var currentEnd = keptSegments[i].SourceEnd;
            var nextStart = keptSegments[i + 1].SourceStart;

            if (nextStart > currentEnd)
            {
                deletedRegions.Add((currentEnd, nextStart));
            }
        }

        // Check for deletion at the end
        var lastSegmentEnd = keptSegments[keptSegments.Count - 1].SourceEnd;
        if (lastSegmentEnd < videoDuration)
        {
            deletedRegions.Add((lastSegmentEnd, videoDuration));
        }

        return deletedRegions;
    }

    /// <summary>
    /// Seeks to a specific pixel position on the timeline.
    /// Converts pixel → VIRTUAL time → SOURCE time for frame lookup.
    /// </summary>
    [RelayCommand]
    public void SeekToPixel(double pixelPosition)
    {
        if (Metrics == null) return;

        // Convert pixel to virtual time (Metrics uses virtual duration)
        var virtualTime = Metrics.PixelToTime(pixelPosition);

        // Clamp to valid range (virtual duration)
        var maxDuration = SegmentManager?.CurrentSegments.TotalDuration ?? VideoMetadata?.Duration ?? TimeSpan.Zero;

        if (virtualTime < TimeSpan.Zero)
            virtualTime = TimeSpan.Zero;
        else if (virtualTime > maxDuration)
            virtualTime = maxDuration;

        // Convert virtual time to source time for frame lookup
        var sourceTime = VirtualToSourceTime(virtualTime);

        CurrentTime = sourceTime;
        OnPropertyChanged(nameof(PlayheadPosition));
    }

    /// <summary>
    /// Start a new selection at the given pixel position.
    /// Selection is in VIRTUAL coordinates (contracted timeline).
    /// </summary>
    [RelayCommand]
    public void StartSelection(double pixelPosition)
    {
        if (Metrics == null) return;
        // Convert pixel to VIRTUAL time (Metrics uses virtual duration)
        var virtualTime = Metrics.PixelToTime(pixelPosition);

        Selection.StartSelection(virtualTime);
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(SelectionStartPixel));
        OnPropertyChanged(nameof(CanDeleteSelection));
    }

    /// <summary>
    /// Update the selection end point at the given pixel position.
    /// Selection is in VIRTUAL coordinates (contracted timeline).
    /// </summary>
    [RelayCommand]
    public void UpdateSelection(double pixelPosition)
    {
        if (Metrics == null || !Selection.IsActive) return;
        // Convert pixel to VIRTUAL time (Metrics uses virtual duration)
        var virtualTime = Metrics.PixelToTime(pixelPosition);
        Selection.UpdateSelection(virtualTime);
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(SelectionEndPixel));
        OnPropertyChanged(nameof(SelectionWidth));
        OnPropertyChanged(nameof(SelectionNormalizedStartPixel));
        OnPropertyChanged(nameof(CanDeleteSelection));
    }

    /// <summary>
    /// Clear the current selection.
    /// </summary>
    [RelayCommand]
    public void ClearSelection()
    {
        Selection.ClearSelection();
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(SelectionStartPixel));
        OnPropertyChanged(nameof(SelectionEndPixel));
        OnPropertyChanged(nameof(SelectionWidth));
        OnPropertyChanged(nameof(SelectionNormalizedStartPixel));
        OnPropertyChanged(nameof(CanDeleteSelection));
    }

    /// <summary>
    /// Loads video metadata and thumbnails.
    /// </summary>
    public void LoadVideo(VideoMetadata metadata, List<ThumbnailData> thumbnails)
    {
        VideoMetadata = metadata;
        Thumbnails = new ObservableCollection<ThumbnailData>(thumbnails);
        CurrentTime = TimeSpan.Zero;
        OnPropertyChanged(nameof(Metrics));
        OnPropertyChanged(nameof(PlayheadPosition));
    }

    /// <summary>
    /// Notify that segments have changed and timeline should refresh
    /// </summary>
    public void NotifySegmentsChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        OnPropertyChanged(nameof(Metrics));
        OnPropertyChanged(nameof(PlayheadPosition));

        // Fire explicit event for timeline control to refresh
        SegmentsChanged?.Invoke(this, EventArgs.Empty);
    }
}
