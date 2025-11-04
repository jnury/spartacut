using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bref.Models;
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

    [ObservableProperty]
    private TimeSpan _currentTime;

    /// <summary>
    /// Event raised when CurrentTime changes.
    /// </summary>
    public event EventHandler<TimeSpan>? CurrentTimeChanged;

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
    /// </summary>
    public TimelineMetrics? Metrics =>
        VideoMetadata != null
            ? new TimelineMetrics
            {
                TotalDuration = VideoMetadata.Duration,
                TimelineWidth = TimelineWidth,
                TimelineHeight = TimelineHeight
            }
            : null;

    /// <summary>
    /// Playhead position in pixels.
    /// </summary>
    public double PlayheadPosition => Metrics?.TimeToPixel(CurrentTime) ?? 0;

    /// <summary>
    /// Seeks to a specific pixel position on the timeline.
    /// </summary>
    [RelayCommand]
    public void SeekToPixel(double pixelPosition)
    {
        if (Metrics == null) return;

        var newTime = Metrics.PixelToTime(pixelPosition);

        // Clamp to valid range
        if (newTime < TimeSpan.Zero)
            newTime = TimeSpan.Zero;
        else if (newTime > VideoMetadata!.Duration)
            newTime = VideoMetadata.Duration;

        CurrentTime = newTime;
        OnPropertyChanged(nameof(PlayheadPosition));
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
}
