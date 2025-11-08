using System;
using System.Threading;
using System.Threading.Tasks;
using Bref.Core.Models;
using Bref.Core.Services;
using Bref.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace Bref.Core.ViewModels;

/// <summary>
/// Main window ViewModel orchestrating video editing operations
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly SegmentManager _segmentManager = new();
    private readonly IPlaybackEngine _playbackEngine;
    private readonly SynchronizationContext? _uiContext;

    [ObservableProperty]
    private TimelineViewModel _timeline = new();

    [ObservableProperty]
    private bool _isPlaying = false;

    private double _volume = 1.0;
    private double _volumeBeforeMute = 1.0;
    private bool _isMuted = false;

    /// <summary>
    /// Audio volume (0.0 to 1.0)
    /// </summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _playbackEngine.SetVolume((float)value);
            }
        }
    }

    /// <summary>
    /// Toggle mute state
    /// </summary>
    public void ToggleMute()
    {
        if (_isMuted)
        {
            // Unmute - restore previous volume
            Volume = _volumeBeforeMute;
            _isMuted = false;
            Log.Debug("Audio unmuted, volume restored to {Volume}", _volumeBeforeMute);
        }
        else
        {
            // Mute - save current volume and set to 0
            _volumeBeforeMute = Volume;
            Volume = 0.0;
            _isMuted = true;
            Log.Debug("Audio muted, previous volume: {Volume}", _volumeBeforeMute);
        }
    }

    /// <summary>
    /// Exposes playback engine for UI binding
    /// </summary>
    public IPlaybackEngine PlaybackEngine => _playbackEngine;

    /// <summary>
    /// Segment manager for playback engine access
    /// </summary>
    public SegmentManager SegmentManager => _segmentManager;

    /// <summary>
    /// Callback to regenerate thumbnails before timeline update
    /// </summary>
    public Func<Task>? RegenerateThumbnailsCallback { get; set; }

    /// <summary>
    /// Constructor - sets up Timeline property change subscription
    /// </summary>
    public MainWindowViewModel(IPlaybackEngine playbackEngine)
    {
        _playbackEngine = playbackEngine ?? throw new ArgumentNullException(nameof(playbackEngine));

        // Capture UI synchronization context
        _uiContext = SynchronizationContext.Current;

        // Subscribe to Timeline property changes to update command states
        Timeline.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Timeline.CanDeleteSelection))
            {
                // Selection changed - update Delete command state
                OnPropertyChanged(nameof(CanDelete));
                DeleteSelectionCommand.NotifyCanExecuteChanged();
            }
        };

        // Subscribe to playback state changes
        _playbackEngine.StateChanged += OnPlaybackStateChanged;
        _playbackEngine.TimeChanged += OnPlaybackTimeChanged;

        // Subscribe to timeline seeks (user clicking timeline)
        Timeline.CurrentTimeChanged += OnTimelineSeek;
    }

    private bool _updatingFromPlayback = false;

    private void OnTimelineSeek(object? sender, TimeSpan time)
    {
        OnTimelineScrubbed(time);
    }

    private void OnTimelineScrubbed(TimeSpan sourceTime)
    {
        // Only seek VlcPlaybackEngine if this wasn't triggered by playback
        if (!_updatingFromPlayback && _playbackEngine.CanPlay)
        {
            // Timeline already provides source time (not virtual time)
            // Seek VLC to source time (with throttling built-in)
            _playbackEngine.Seek(sourceTime);
        }
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState state)
    {
        // Marshal to UI thread (PlaybackEngine fires from Timer thread)
        if (_uiContext != null)
        {
            _uiContext.Post(_ =>
            {
                IsPlaying = state == PlaybackState.Playing;
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanPause));
                PlayCommand.NotifyCanExecuteChanged();
                PauseCommand.NotifyCanExecuteChanged();
            }, null);
        }
        else
        {
            // Fallback if no UI context (e.g., in tests)
            IsPlaying = state == PlaybackState.Playing;
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(CanPause));
            PlayCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnPlaybackTimeChanged(object? sender, TimeSpan time)
    {
        // Marshal to UI thread (PlaybackEngine fires from Timer thread)
        if (_uiContext != null)
        {
            _uiContext.Post(_ =>
            {
                // Set flag to prevent circular update
                _updatingFromPlayback = true;
                try
                {
                    // Update timeline current time
                    Timeline.CurrentTime = time;
                }
                finally
                {
                    _updatingFromPlayback = false;
                }
            }, null);
        }
        else
        {
            // Fallback if no UI context (e.g., in tests)
            _updatingFromPlayback = true;
            try
            {
                Timeline.CurrentTime = time;
            }
            finally
            {
                _updatingFromPlayback = false;
            }
        }
    }

    /// <summary>
    /// Can delete? (selection is valid)
    /// </summary>
    public bool CanDelete => Timeline.CanDeleteSelection;

    /// <summary>
    /// Can undo?
    /// </summary>
    public bool CanUndo => _segmentManager.CanUndo;

    /// <summary>
    /// Can redo?
    /// </summary>
    public bool CanRedo => _segmentManager.CanRedo;

    /// <summary>
    /// Can play? (video loaded and not playing)
    /// </summary>
    public bool CanPlay => _playbackEngine.CanPlay && !IsPlaying;

    /// <summary>
    /// Can pause? (currently playing)
    /// </summary>
    public bool CanPause => IsPlaying;

    /// <summary>
    /// Current virtual duration (after deletions)
    /// </summary>
    public TimeSpan VirtualDuration => _segmentManager.CurrentSegments.TotalDuration;

    /// <summary>
    /// Number of segments
    /// </summary>
    public int SegmentCount => _segmentManager.CurrentSegments.SegmentCount;

    /// <summary>
    /// Initialize with video metadata (no FrameCache needed with VLC)
    /// </summary>
    public void InitializeVideo(VideoMetadata metadata)
    {
        _segmentManager.Initialize(metadata.Duration);
        Timeline.VideoMetadata = metadata;
        Timeline.SegmentManager = _segmentManager; // Connect SegmentManager to Timeline!

        // Initialize VLC playback engine
        _playbackEngine.Initialize(metadata.FilePath, _segmentManager, metadata);

        OnPropertyChanged(nameof(CanPlay));
        PlayCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Delete currently selected segment
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task DeleteSelection()
    {
        if (!Timeline.Selection.IsValid)
            return;

        // Selection coordinates are already in VIRTUAL time (contracted timeline)
        var virtualStart = Timeline.Selection.NormalizedStart;
        var virtualEnd = Timeline.Selection.NormalizedEnd;

        // Delete segment
        _segmentManager.DeleteSegment(virtualStart, virtualEnd);

        // Clear selection
        Timeline.ClearSelectionCommand.Execute(null);

        // Update UI
        await UpdateAfterEditAsync();
    }

    /// <summary>
    /// Undo last operation
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public async Task Undo()
    {
        _segmentManager.Undo();
        await UpdateAfterEditAsync();
    }

    /// <summary>
    /// Redo last undone operation
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public async Task Redo()
    {
        _segmentManager.Redo();
        await UpdateAfterEditAsync();
    }

    private async Task UpdateAfterEditAsync()
    {
        // Suppress timeline updates during thumbnail regeneration
        Timeline.BeginUpdate();

        try
        {
            // Regenerate thumbnails BEFORE updating timeline visual
            if (RegenerateThumbnailsCallback != null)
            {
                await RegenerateThumbnailsCallback();
            }
        }
        finally
        {
            // Resume timeline updates
            Timeline.EndUpdate();
        }

        // Update timeline to reflect new virtual duration and deleted segments
        Timeline.NotifySegmentsChanged();

        // Notify command state changes
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(VirtualDuration));
        OnPropertyChanged(nameof(SegmentCount));
    }

    /// <summary>
    /// Play command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    public void Play()
    {
        _playbackEngine.Play();
    }

    /// <summary>
    /// Pause command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    public void Pause()
    {
        _playbackEngine.Pause();
    }

    /// <summary>
    /// Stop command - pauses and resets to beginning
    /// </summary>
    [RelayCommand]
    public void Stop()
    {
        _playbackEngine.Pause();
        _playbackEngine.Seek(TimeSpan.Zero);
        Timeline.CurrentTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _playbackEngine?.Dispose();
    }
}
