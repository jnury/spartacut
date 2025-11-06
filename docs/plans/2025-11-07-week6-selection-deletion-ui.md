# Week 6 Implementation Plan: Selection & Deletion UI

**Date:** 2025-11-07
**Version:** 1.0
**Status:** Ready for Implementation
**Estimated Effort:** 25 hours

## Goal

Implement the user interface for selecting and deleting video segments on the timeline. This connects the virtual timeline backend (Week 5) to the UI, enabling users to visually select ranges and delete them with instant feedback.

## Context

**What's Done (Weeks 1-5):**
- ✅ FFmpeg integration and hardware acceleration
- ✅ Video loading and metadata extraction
- ✅ Waveform and thumbnail generation
- ✅ Frame cache with LRU eviction
- ✅ Video player control with smooth scrubbing
- ✅ Timeline UI rendering (waveform + thumbnails)
- ✅ **Virtual timeline core logic (SegmentManager, SegmentList, EditHistory)**

**Current Version:** 0.5.0

**This Week's Focus:**
- Build the **selection UI** on top of the existing timeline
- Enable click-and-drag to select ranges
- Wire Delete key to SegmentManager
- Add visual feedback for deletions
- Integrate status bar showing duration and segment count

## Prerequisites

- Week 5 completed (SegmentManager fully tested and working)
- Understanding of Avalonia UI event handling
- Understanding of MVVM pattern with CommunityToolkit.Mvvm

---

## Tasks Breakdown

### Task 1: Implement TimelineSelection Model
**Estimated Time:** 2 hours
**Test File:** `src/Bref.Tests/Models/TimelineSelectionTests.cs`
**Implementation File:** `src/Bref/Models/TimelineSelection.cs`

#### Purpose
Model to track the currently selected range on the timeline (in virtual time).

#### Test Cases to Write First:
```csharp
[Fact]
public void Constructor_InitializesWithNoSelection()
{
    // New TimelineSelection should have IsActive = false
}

[Fact]
public void StartSelection_SetsStartTime()
{
    // StartSelection(time) should set SelectionStart and mark as active
}

[Fact]
public void UpdateSelection_SetsEndTime()
{
    // UpdateSelection(time) should set SelectionEnd
}

[Fact]
public void Duration_CalculatesCorrectly()
{
    // Duration should return End - Start (or Start - End if dragging backwards)
}

[Fact]
public void NormalizedRange_ReturnsStartBeforeEnd()
{
    // NormalizedStart/NormalizedEnd should always return (smaller, larger)
}

[Fact]
public void ClearSelection_ResetsState()
{
    // ClearSelection() should set IsActive = false
}

[Fact]
public void IsValid_ReturnsFalseForZeroDuration()
{
    // Selection with same start/end should be invalid
}
```

#### Implementation Spec:
```csharp
namespace Bref.Models
{
    /// <summary>
    /// Represents a user's selection on the timeline
    /// </summary>
    public class TimelineSelection
    {
        /// <summary>
        /// Whether a selection is currently active
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Start time of selection (virtual timeline)
        /// </summary>
        public TimeSpan SelectionStart { get; private set; }

        /// <summary>
        /// End time of selection (virtual timeline)
        /// May be before or after SelectionStart depending on drag direction
        /// </summary>
        public TimeSpan SelectionEnd { get; private set; }

        /// <summary>
        /// Duration of selection (always positive)
        /// </summary>
        public TimeSpan Duration =>
            TimeSpan.FromSeconds(Math.Abs((SelectionEnd - SelectionStart).TotalSeconds));

        /// <summary>
        /// Normalized start (always the earlier time)
        /// </summary>
        public TimeSpan NormalizedStart =>
            SelectionStart < SelectionEnd ? SelectionStart : SelectionEnd;

        /// <summary>
        /// Normalized end (always the later time)
        /// </summary>
        public TimeSpan NormalizedEnd =>
            SelectionStart < SelectionEnd ? SelectionEnd : SelectionStart;

        /// <summary>
        /// Whether the selection is valid (non-zero duration)
        /// </summary>
        public bool IsValid => IsActive && Duration.TotalSeconds > 0.01; // 10ms minimum

        /// <summary>
        /// Start a new selection at the given time
        /// </summary>
        public void StartSelection(TimeSpan time)
        {
            IsActive = true;
            SelectionStart = time;
            SelectionEnd = time;
        }

        /// <summary>
        /// Update the selection end point
        /// </summary>
        public void UpdateSelection(TimeSpan time)
        {
            SelectionEnd = time;
        }

        /// <summary>
        /// Clear the current selection
        /// </summary>
        public void ClearSelection()
        {
            IsActive = false;
            SelectionStart = TimeSpan.Zero;
            SelectionEnd = TimeSpan.Zero;
        }
    }
}
```

#### Acceptance Criteria:
- ✅ All 7 tests pass
- ✅ Duration always positive
- ✅ Normalized range handles backward dragging
- ✅ IsValid checks minimum duration
- ✅ XML documentation complete

---

### Task 2: Add Selection Properties to TimelineViewModel
**Estimated Time:** 2 hours
**Test File:** `src/Bref.Tests/ViewModels/TimelineViewModelTests.cs` (add to existing)
**Implementation File:** `src/Bref/ViewModels/TimelineViewModel.cs` (modify existing)

#### Purpose
Extend TimelineViewModel to manage selection state and expose it to the UI.

#### New Properties and Commands:
```csharp
[ObservableProperty]
private TimelineSelection _selection = new();

/// <summary>
/// Selection start position in pixels
/// </summary>
public double SelectionStartPixel =>
    Selection.IsActive && Metrics != null
        ? Metrics.TimeToPixel(Selection.SelectionStart)
        : 0;

/// <summary>
/// Selection end position in pixels
/// </summary>
public double SelectionEndPixel =>
    Selection.IsActive && Metrics != null
        ? Metrics.TimeToPixel(Selection.SelectionEnd)
        : 0;

/// <summary>
/// Selection width in pixels (always positive)
/// </summary>
public double SelectionWidth => Math.Abs(SelectionEndPixel - SelectionStartPixel);

/// <summary>
/// Selection normalized start in pixels (always leftmost)
/// </summary>
public double SelectionNormalizedStartPixel =>
    Math.Min(SelectionStartPixel, SelectionEndPixel);

[RelayCommand]
public void StartSelection(double pixelPosition)
{
    if (Metrics == null) return;
    var time = Metrics.PixelToTime(pixelPosition);
    Selection.StartSelection(time);
    OnPropertyChanged(nameof(Selection));
    OnPropertyChanged(nameof(SelectionStartPixel));
}

[RelayCommand]
public void UpdateSelection(double pixelPosition)
{
    if (Metrics == null || !Selection.IsActive) return;
    var time = Metrics.PixelToTime(pixelPosition);
    Selection.UpdateSelection(time);
    OnPropertyChanged(nameof(Selection));
    OnPropertyChanged(nameof(SelectionEndPixel));
    OnPropertyChanged(nameof(SelectionWidth));
    OnPropertyChanged(nameof(SelectionNormalizedStartPixel));
}

[RelayCommand]
public void ClearSelection()
{
    Selection.ClearSelection();
    OnPropertyChanged(nameof(Selection));
    OnPropertyChanged(nameof(SelectionStartPixel));
    OnPropertyChanged(nameof(SelectionEndPixel));
    OnPropertyChanged(nameof(SelectionWidth));
    OnPropertyChanged(nameof(SelectionNormalizedStartPixel));
}
```

#### Test Cases:
```csharp
[Fact]
public void StartSelection_UpdatesSelectionProperties()
{
    // Starting selection should update pixel properties
}

[Fact]
public void UpdateSelection_UpdatesWidth()
{
    // Updating selection should recalculate width
}

[Fact]
public void ClearSelection_ResetsProperties()
{
    // Clearing selection should reset all pixel properties
}

[Fact]
public void SelectionNormalizedStartPixel_AlwaysLeftmost()
{
    // Normalized start should be leftmost regardless of drag direction
}
```

#### Acceptance Criteria:
- ✅ All 4 tests pass
- ✅ Property notifications fire correctly
- ✅ Pixel calculations use TimelineMetrics
- ✅ Works with backward dragging
- ✅ XML documentation complete

---

### Task 3: Update TimelineControl for Selection Rendering
**Estimated Time:** 4 hours
**Files:** `src/Bref/Controls/TimelineControl.axaml.cs`

#### Purpose
Modify TimelineControl to:
1. Distinguish between seek (single click) and selection (click-and-drag)
2. Render selection highlight
3. Handle selection drag gestures

#### Pointer Event Logic:
```csharp
// Track drag start position to distinguish click from drag
private Point? _pointerDownPosition;
private const double DragThreshold = 5.0; // pixels

private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (_viewModel?.VideoMetadata == null)
        return;

    var point = e.GetPosition(this);
    _pointerDownPosition = point;
    _isDragging = false; // Don't start dragging yet
    e.Handled = true;
}

private void OnPointerMoved(object? sender, PointerEventArgs e)
{
    if (_pointerDownPosition == null || _viewModel?.VideoMetadata == null)
        return;

    var point = e.GetPosition(this);

    // Check if moved beyond threshold (selection drag vs. seek click)
    var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

    if (!_isDragging && distance > DragThreshold)
    {
        // Start selection
        _isDragging = true;
        _viewModel.StartSelectionCommand.Execute(_pointerDownPosition.Value.X);
    }

    if (_isDragging)
    {
        // Update selection
        _viewModel.UpdateSelectionCommand.Execute(point.X);
        InvalidateVisual();
    }

    e.Handled = true;
}

private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
{
    if (_viewModel == null || _pointerDownPosition == null)
        return;

    var point = e.GetPosition(this);
    var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

    if (distance <= DragThreshold)
    {
        // Single click - seek to position
        _viewModel.SeekToPixel(point.X);
        _viewModel.ClearSelectionCommand.Execute(null);
    }
    else if (_viewModel.Selection.IsValid)
    {
        // Valid selection completed
        // Selection remains active for Delete operation
    }
    else
    {
        // Invalid selection (too small)
        _viewModel.ClearSelectionCommand.Execute(null);
    }

    _pointerDownPosition = null;
    _isDragging = false;
    InvalidateVisual();
    e.Handled = true;
}
```

#### Selection Rendering (add to TimelineRenderOperation):
```csharp
// In TimelineRenderOperation.Render() method, after drawing waveform/thumbnails:

// Draw selection highlight
if (_viewModel.Selection.IsActive)
{
    var selectionRect = new SKRect(
        (float)_viewModel.SelectionNormalizedStartPixel,
        0,
        (float)(_viewModel.SelectionNormalizedStartPixel + _viewModel.SelectionWidth),
        (float)_bounds.Height
    );

    // Semi-transparent blue overlay
    using var selectionPaint = new SKPaint
    {
        Color = new SKColor(100, 150, 255, 80), // Light blue, 30% opacity
        Style = SKPaintStyle.Fill
    };
    canvas.DrawRect(selectionRect, selectionPaint);

    // Selection border
    using var borderPaint = new SKPaint
    {
        Color = new SKColor(100, 150, 255, 200), // Light blue, 80% opacity
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2
    };
    canvas.DrawRect(selectionRect, borderPaint);

    // Draw handles (optional, for visual clarity)
    DrawSelectionHandle(canvas, selectionRect.Left, selectionRect.Height);
    DrawSelectionHandle(canvas, selectionRect.Right, selectionRect.Height);
}

private void DrawSelectionHandle(SKCanvas canvas, float x, float height)
{
    var handleRect = new SKRect(x - 5, 0, x + 5, height);
    using var handlePaint = new SKPaint
    {
        Color = new SKColor(100, 150, 255, 255),
        Style = SKPaintStyle.Fill
    };
    canvas.DrawRect(handleRect, handlePaint);
}
```

#### Acceptance Criteria:
- ✅ Single click seeks timeline
- ✅ Click-and-drag creates selection
- ✅ Selection renders with blue overlay
- ✅ Small drags (<5px) treated as clicks
- ✅ Selection cleared on next click
- ✅ Visual handles on selection edges

---

### Task 4: Create MainWindowViewModel
**Estimated Time:** 3 hours
**Test File:** `src/Bref.Tests/ViewModels/MainWindowViewModelTests.cs`
**Implementation File:** `src/Bref/ViewModels/MainWindowViewModel.cs`

#### Purpose
Create a main ViewModel to orchestrate SegmentManager, TimelineViewModel, and handle Delete command.

#### Implementation Spec:
```csharp
namespace Bref.ViewModels
{
    /// <summary>
    /// Main window ViewModel orchestrating video editing operations
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly SegmentManager _segmentManager = new();

        [ObservableProperty]
        private TimelineViewModel _timeline = new();

        [ObservableProperty]
        private string _statusText = "No video loaded";

        /// <summary>
        /// Can delete? (selection is valid)
        /// </summary>
        public bool CanDelete => Timeline.Selection.IsValid;

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
            UpdateStatusBar();
        }

        /// <summary>
        /// Delete currently selected segment
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDelete))]
        public void DeleteSelection()
        {
            if (!Timeline.Selection.IsValid)
                return;

            var start = Timeline.Selection.NormalizedStart;
            var end = Timeline.Selection.NormalizedEnd;

            // Delete segment
            _segmentManager.DeleteSegment(start, end);

            // Clear selection
            Timeline.ClearSelectionCommand.Execute(null);

            // Update UI
            UpdateAfterEdit();
        }

        /// <summary>
        /// Undo last operation
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndo))]
        public void Undo()
        {
            _segmentManager.Undo();
            UpdateAfterEdit();
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRedo))]
        public void Redo()
        {
            _segmentManager.Redo();
            UpdateAfterEdit();
        }

        private void UpdateAfterEdit()
        {
            // Update timeline to reflect new virtual duration
            // (Timeline will need to adapt to show contracted segments)

            // Update status bar
            UpdateStatusBar();

            // Notify command state changes
            DeleteSelectionCommand.NotifyCanExecuteChanged();
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();

            OnPropertyChanged(nameof(VirtualDuration));
            OnPropertyChanged(nameof(SegmentCount));
        }

        private void UpdateStatusBar()
        {
            var duration = VirtualDuration.ToString(@"hh\:mm\:ss");
            StatusText = $"Duration: {duration} | Segments: {SegmentCount}";
        }
    }
}
```

#### Test Cases:
```csharp
[Fact]
public void InitializeVideo_SetsUpSegmentManager()
{
    // Initialize should create single segment spanning video
}

[Fact]
public void DeleteSelection_CallsSegmentManager()
{
    // Delete should delegate to SegmentManager.DeleteSegment()
}

[Fact]
public void DeleteSelection_ClearsSelection()
{
    // After deletion, selection should be cleared
}

[Fact]
public void DeleteSelection_UpdatesVirtualDuration()
{
    // Virtual duration should decrease after deletion
}

[Fact]
public void Undo_RestoresState()
{
    // Undo should restore previous state
}

[Fact]
public void UpdateStatusBar_ShowsDurationAndSegments()
{
    // Status bar should display current duration and segment count
}

[Fact]
public void CanDelete_FalseWhenNoSelection()
{
    // Delete command should be disabled without valid selection
}
```

#### Acceptance Criteria:
- ✅ All 7 tests pass
- ✅ Delete command wired to SegmentManager
- ✅ Status bar updates after operations
- ✅ Command CanExecute logic correct
- ✅ Property notifications fire
- ✅ XML documentation complete

---

### Task 5: Wire MainWindowViewModel to MainWindow
**Estimated Time:** 3 hours
**Files:**
- `src/Bref/Views/MainWindow.axaml` (add controls)
- `src/Bref/Views/MainWindow.axaml.cs` (wire ViewModel)

#### XAML Updates:
```xml
<!-- Add to MainWindow.axaml -->

<!-- Delete Button (Toolbar) -->
<Button Name="DeleteButton"
        Content="Delete Selection"
        Command="{Binding DeleteSelectionCommand}"
        HotKey="Delete" />

<!-- Undo Button -->
<Button Name="UndoButton"
        Content="Undo"
        Command="{Binding UndoCommand}"
        HotKey="Ctrl+Z" />

<!-- Redo Button -->
<Button Name="RedoButton"
        Content="Redo"
        Command="{Binding RedoCommand}"
        HotKey="Ctrl+Y" />

<!-- Status Bar (Bottom of window) -->
<TextBlock Name="StatusBar"
           Text="{Binding StatusText}"
           HorizontalAlignment="Left"
           VerticalAlignment="Center"
           Margin="10,5" />
```

#### Code-Behind Updates:
```csharp
// In MainWindow.axaml.cs

private MainWindowViewModel? _viewModel;

public MainWindow()
{
    InitializeComponent();

    // Initialize ViewModel
    _viewModel = new MainWindowViewModel();
    DataContext = _viewModel;

    // Wire timeline ViewModel
    if (Timeline != null)
    {
        Timeline.DataContext = _viewModel.Timeline;
    }

    // ... existing FFmpeg initialization ...
}

private async Task LoadVideoFile(string filePath)
{
    // ... existing load logic ...

    // After successful load, initialize ViewModel
    if (_viewModel != null && videoMetadata != null)
    {
        _viewModel.InitializeVideo(videoMetadata);
    }

    // ... rest of existing code ...
}
```

#### Acceptance Criteria:
- ✅ MainWindowViewModel set as DataContext
- ✅ Delete button enabled only with valid selection
- ✅ Undo/Redo buttons enabled based on history state
- ✅ Status bar shows duration and segment count
- ✅ Hotkeys work (Delete, Ctrl+Z, Ctrl+Y)
- ✅ Timeline control bound to Timeline property

---

### Task 6: Update Timeline Rendering After Deletions
**Estimated Time:** 5 hours
**Files:**
- `src/Bref/Models/TimelineMetrics.cs` (update for virtual timeline)
- `src/Bref/ViewModels/TimelineViewModel.cs` (adapt to segments)

#### Challenge
Currently, TimelineMetrics assumes a continuous timeline from 0 to Duration. After deletions, the timeline should show only kept segments, contracted to remove deleted portions.

#### Solution Approach

**Option A: Virtual Timeline Only (Simpler - Recommended for Week 6)**
- Timeline always represents 0 to VirtualDuration
- Thumbnails/waveform are generated from source video but displayed in virtual space
- Time conversion: UI positions → Virtual time → Source time (via SegmentManager)

**Option B: Segment-Aware Timeline (More Complex)**
- Timeline explicitly shows gaps between segments
- Each segment rendered separately with visual separators
- More accurate but requires significant rendering changes

**Week 6 Implementation: Use Option A**

#### TimelineViewModel Updates:
```csharp
/// <summary>
/// Segment manager for virtual timeline calculations
/// </summary>
public SegmentManager? SegmentManager { get; set; }

/// <summary>
/// Timeline metrics using virtual duration
/// </summary>
public TimelineMetrics? Metrics =>
    SegmentManager != null
        ? new TimelineMetrics
        {
            TotalDuration = SegmentManager.CurrentSegments.TotalDuration, // Virtual duration
            TimelineWidth = TimelineWidth,
            TimelineHeight = TimelineHeight
        }
        : VideoMetadata != null
            ? new TimelineMetrics
            {
                TotalDuration = VideoMetadata.Duration, // Fallback to source duration
                TimelineWidth = TimelineWidth,
                TimelineHeight = TimelineHeight
            }
            : null;

/// <summary>
/// Convert virtual time to source time for frame lookup
/// </summary>
public TimeSpan VirtualToSourceTime(TimeSpan virtualTime)
{
    return SegmentManager?.CurrentSegments.VirtualToSourceTime(virtualTime) ?? virtualTime;
}

/// <summary>
/// Seeks to a specific pixel position (now in virtual time)
/// </summary>
[RelayCommand]
public void SeekToPixel(double pixelPosition)
{
    if (Metrics == null) return;

    var virtualTime = Metrics.PixelToTime(pixelPosition);
    var sourceTime = VirtualToSourceTime(virtualTime);

    // Clamp to virtual range
    if (virtualTime < TimeSpan.Zero)
        virtualTime = TimeSpan.Zero;
    else if (SegmentManager != null && virtualTime > SegmentManager.CurrentSegments.TotalDuration)
        virtualTime = SegmentManager.CurrentSegments.TotalDuration;

    CurrentTime = sourceTime; // Store source time for frame lookup
    OnPropertyChanged(nameof(PlayheadPosition));
}
```

#### MainWindowViewModel Updates:
```csharp
public void InitializeVideo(VideoMetadata metadata)
{
    _segmentManager.Initialize(metadata.Duration);
    Timeline.VideoMetadata = metadata;
    Timeline.SegmentManager = _segmentManager; // Wire segment manager
    UpdateStatusBar();
}
```

#### Acceptance Criteria:
- ✅ Timeline contracts after deletions
- ✅ Virtual duration displayed correctly
- ✅ Playhead seeks correctly in virtual space
- ✅ Frame lookup uses source time conversion
- ✅ Selection works in virtual space

---

### Task 7: Integration Testing
**Estimated Time:** 3 hours
**Test File:** `src/Bref.Tests/Integration/SelectionDeletionIntegrationTests.cs`

#### Test Scenarios:
```csharp
[Fact]
public async Task Scenario_SelectAndDelete_UpdatesTimeline()
{
    // 1. Load video (60 seconds)
    // 2. Select range [10s - 20s] (virtual)
    // 3. Delete selection
    // 4. Verify virtual duration = 50s
    // 5. Verify segment count = 2
    // 6. Verify status bar updated
}

[Fact]
public async Task Scenario_DeleteUndo_RestoresTimeline()
{
    // 1. Load video (60 seconds)
    // 2. Delete [10s - 20s]
    // 3. Verify duration = 50s
    // 4. Undo
    // 5. Verify duration = 60s
    // 6. Verify segment count = 1
}

[Fact]
public async Task Scenario_MultipleDeletes_UpdatesCorrectly()
{
    // 1. Load video (90 seconds)
    // 2. Delete [10s - 20s] → 80s
    // 3. Delete [20s - 30s] virtual → 70s
    // 4. Verify segment count = 3
    // 5. Verify virtual-to-source conversion correct
}

[Fact]
public async Task Scenario_SmallSelection_Ignored()
{
    // 1. Load video
    // 2. Select range of 1 pixel (< 10ms)
    // 3. Verify IsValid = false
    // 4. Verify Delete button disabled
}

[Fact]
public async Task Scenario_BackwardDrag_Works()
{
    // 1. Start selection at 30s
    // 2. Drag backward to 10s
    // 3. Verify NormalizedStart = 10s, NormalizedEnd = 30s
    // 4. Delete works correctly
}
```

#### Acceptance Criteria:
- ✅ All 5 integration tests pass
- ✅ Selection, deletion, undo work together
- ✅ Timeline updates correctly after edits
- ✅ Status bar reflects current state
- ✅ Edge cases handled (small selections, backward drags)

---

### Task 8: Update Version and Documentation
**Estimated Time:** 1 hour

#### Version Update:
- Increment version to **0.6.0** (major feature: selection & deletion UI)
- Update `src/Bref/Bref.csproj`

#### Documentation:
- Add Week 6 summary to plan file
- Update CLAUDE.md with any new learnings
- Document UI interaction patterns

#### Acceptance Criteria:
- ✅ Version updated to 0.6.0
- ✅ Week 6 summary complete
- ✅ All new public APIs documented

---

### Task 9: Manual Testing and Polish
**Estimated Time:** 2 hours

#### Manual Test Checklist:
- [ ] Load a video
- [ ] Single click seeks correctly
- [ ] Click-and-drag creates visible selection
- [ ] Selection highlight renders correctly
- [ ] Delete button enabled with selection
- [ ] Delete key removes selected segment
- [ ] Timeline contracts after deletion
- [ ] Status bar updates correctly
- [ ] Undo restores previous state
- [ ] Redo works after undo
- [ ] Multiple deletions work
- [ ] Playhead stays within valid range after deletions

#### Polish Items:
- Smooth selection animation
- Selection color matches app theme
- Handle edge cases (selection at very start/end)
- Proper cursor feedback during drag

#### Acceptance Criteria:
- ✅ All manual tests pass
- ✅ UI feels responsive and smooth
- ✅ No visual glitches
- ✅ Edge cases handled gracefully

---

## File Summary

### New Files Created:
```
src/Bref/Models/
  └── TimelineSelection.cs                    (~80 lines)

src/Bref/ViewModels/
  └── MainWindowViewModel.cs                  (~150 lines)

src/Bref.Tests/Models/
  └── TimelineSelectionTests.cs               (~120 lines)

src/Bref.Tests/ViewModels/
  └── MainWindowViewModelTests.cs             (~150 lines)

src/Bref.Tests/Integration/
  └── SelectionDeletionIntegrationTests.cs    (~200 lines)
```

### Modified Files:
```
src/Bref/ViewModels/
  └── TimelineViewModel.cs                    (+80 lines)

src/Bref/Controls/
  └── TimelineControl.axaml.cs                (+120 lines)

src/Bref/Views/
  ├── MainWindow.axaml                        (+30 lines)
  └── MainWindow.axaml.cs                     (+20 lines)

src/Bref/Models/
  └── TimelineMetrics.cs                      (may need updates)
```

**Total New Code:** ~950 lines (including tests)

---

## Testing Strategy

### Unit Tests:
- **Task 1:** TimelineSelection - 7 tests
- **Task 2:** TimelineViewModel selection - 4 tests
- **Task 4:** MainWindowViewModel - 7 tests

**Total Unit Tests:** 18 tests

### Integration Tests (Task 7):
- **Complex scenarios:** 5 tests

**Total Integration Tests:** 5 tests

### Manual Tests (Task 9):
- **User interaction flows:** 12 test cases

---

## Success Criteria

### Functional:
- ✅ User can select ranges by clicking and dragging
- ✅ Selection renders with visual highlight
- ✅ Delete key removes selected segment
- ✅ Timeline contracts to show virtual duration
- ✅ Status bar updates after operations
- ✅ Undo/Redo buttons work correctly
- ✅ All unit tests pass
- ✅ All integration tests pass

### Non-Functional:
- ✅ Selection feels responsive (<16ms update)
- ✅ Deletion operation completes in <100ms
- ✅ UI remains smooth during operations
- ✅ No visual glitches or flicker

### Documentation:
- ✅ All public APIs documented
- ✅ UI interaction patterns documented
- ✅ Week 6 summary complete

---

## Dependencies

### Required:
- ✅ Week 5 complete (SegmentManager working)
- ✅ Avalonia UI 11.x
- ✅ CommunityToolkit.Mvvm
- ✅ SkiaSharp (for rendering)

### No new NuGet packages required

---

## Risk Mitigation

### Risk: Timeline rendering performance after many deletions
**Mitigation:** Use virtual timeline approach (Option A), defer segment-aware rendering to later
**Contingency:** Profile rendering, optimize if needed

### Risk: Selection feels laggy
**Mitigation:** Throttle selection updates, use efficient rendering
**Contingency:** Reduce selection visual complexity

### Risk: Time conversion bugs between virtual and source
**Mitigation:** Comprehensive integration tests, reuse tested SegmentManager logic
**Contingency:** Add debug visualization for time conversion

### Risk: Undo/Redo state management complexity
**Mitigation:** Delegate entirely to SegmentManager (already tested)
**Contingency:** Add extra validation layer in ViewModel

---

## Implementation Order

1. **Task 1:** TimelineSelection model (foundation)
2. **Task 2:** TimelineViewModel selection properties
3. **Task 4:** MainWindowViewModel (can partially parallel with Task 3)
4. **Task 3:** TimelineControl selection rendering
5. **Task 5:** Wire MainWindowViewModel to MainWindow
6. **Task 6:** Timeline virtual duration updates
7. **Task 7:** Integration tests
8. **Task 8:** Version & documentation
9. **Task 9:** Manual testing & polish

**Critical Path:** Task 1 → Task 2 → Task 3 → Task 5 → Task 6 → Task 7

---

## Notes for Implementation

### MVVM Best Practices:
- ViewModels should not reference Views (UI controls)
- Use RelayCommand for all user actions
- Observable properties for all data-bound values
- CanExecute predicates for command state

### Avalonia UI Patterns:
- Custom rendering with ICustomDrawOperation
- Pointer events for mouse/touch input
- Data binding with {Binding} syntax
- HotKey for keyboard shortcuts

### Performance Considerations:
- Selection updates should not trigger frame cache invalidation
- Timeline rendering should be efficient (cached where possible)
- Property change notifications should be minimal

---

## Completion Checklist

Before marking Week 6 complete:

- [ ] All 18 unit tests passing
- [ ] All 5 integration tests passing
- [ ] Manual test checklist complete
- [ ] Build succeeds with no warnings
- [ ] Version updated to 0.6.0
- [ ] XML documentation complete
- [ ] Week 6 summary written
- [ ] Ready for Week 7 (Playback Engine)

---

## Estimated Timeline

**Day 1 (5 hours):**
- Task 1: TimelineSelection model (2 hours)
- Task 2: TimelineViewModel updates (2 hours)
- Task 4: MainWindowViewModel (1 hour)

**Day 2 (6 hours):**
- Task 4: MainWindowViewModel complete (2 hours)
- Task 3: TimelineControl selection (4 hours)

**Day 3 (5 hours):**
- Task 5: Wire MainWindow (3 hours)
- Task 6: Virtual timeline updates (2 hours)

**Day 4 (5 hours):**
- Task 6: Virtual timeline complete (3 hours)
- Task 7: Integration tests (2 hours)

**Day 5 (4 hours):**
- Task 7: Integration tests complete (1 hour)
- Task 8: Version & docs (1 hour)
- Task 9: Manual testing & polish (2 hours)

**Total:** 25 hours over 5 days (5 hours/day)

---

## Next Week Preview (Week 7)

**Week 7 Focus:** Playback Engine
- Implement PlaybackEngine service
- Play/Pause functionality
- Continuous playback through multiple segments
- Frame preloading near segment boundaries
- Audio synchronization
- Handle edge cases (end of video, segment boundaries)

**Dependency:** Week 6 selection UI must be complete and functional

---

**End of Week 6 Implementation Plan**

This plan provides a complete roadmap for implementing selection and deletion UI, connecting the virtual timeline backend to the user interface.
