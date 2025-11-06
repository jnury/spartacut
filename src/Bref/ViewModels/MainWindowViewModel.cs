using System;
using System.Threading.Tasks;
using Bref.Models;
using Bref.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bref.ViewModels;

/// <summary>
/// Main window ViewModel orchestrating video editing operations
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly SegmentManager _segmentManager = new();

    [ObservableProperty]
    private TimelineViewModel _timeline = new();

    /// <summary>
    /// Callback to regenerate thumbnails before timeline update
    /// </summary>
    public Func<Task>? RegenerateThumbnailsCallback { get; set; }

    /// <summary>
    /// Constructor - sets up Timeline property change subscription
    /// </summary>
    public MainWindowViewModel()
    {
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
    /// Current virtual duration (after deletions)
    /// </summary>
    public TimeSpan VirtualDuration => _segmentManager.CurrentSegments.TotalDuration;

    /// <summary>
    /// Number of segments
    /// </summary>
    public int SegmentCount => _segmentManager.CurrentSegments.SegmentCount;

    /// <summary>
    /// Initialize with video duration
    /// </summary>
    public void InitializeVideo(VideoMetadata metadata)
    {
        _segmentManager.Initialize(metadata.Duration);
        Timeline.VideoMetadata = metadata;
        Timeline.SegmentManager = _segmentManager; // Connect SegmentManager to Timeline!
    }

    /// <summary>
    /// Delete currently selected segment
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async void DeleteSelection()
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
    public async void Undo()
    {
        _segmentManager.Undo();
        await UpdateAfterEditAsync();
    }

    /// <summary>
    /// Redo last undone operation
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public async void Redo()
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
}
