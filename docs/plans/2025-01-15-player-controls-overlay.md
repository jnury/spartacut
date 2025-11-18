# Video Player Controls Overlay Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace toolbar playback controls with YouTube-style overlay controls at the bottom of the video player, with segment-aware playback logic.

**Architecture:** Create a new PlayerControlsOverlay UserControl that renders on top of VlcPlayerControl. Wire to existing MainWindowViewModel commands. Add segment-aware logic to skip deleted regions during playback, stop, and skip operations. Implement auto-fade behavior on mouse idle.

**Tech Stack:** Avalonia 11.x XAML, C# .NET 8, MVVM with CommunityToolkit.Mvvm, existing SegmentManager for segment awareness

---

## Task 1: Add Skip Commands to ViewModel

**Files:**
- Modify: `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`
- Reference: `src/SpartaCut.Core/Services/SegmentManager.cs` (for segment awareness)

**Step 1: Add Skip10SecondsBackward command**

Add after existing playback commands (around line 200):

```csharp
[RelayCommand]
private void Skip10SecondsBackward()
{
    if (_playbackEngine?.CurrentPosition == null) return;

    var currentTime = _playbackEngine.CurrentPosition.Value;
    var targetTime = currentTime - TimeSpan.FromSeconds(10);

    // Clamp to valid range
    if (targetTime < TimeSpan.Zero)
        targetTime = TimeSpan.Zero;

    // If we have segment manager, ensure we land on a visible frame
    if (_segmentManager != null)
    {
        var sourceTime = _timelineViewModel.VirtualToSourceTime(targetTime);
        var virtualTime = _timelineViewModel.SourceToVirtualTime(sourceTime);

        // If target is in deleted region, find previous visible frame
        if (virtualTime == null)
        {
            var segments = _segmentManager.CurrentSegments.KeptSegments;
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (segments[i].SourceEnd <= sourceTime)
                {
                    targetTime = segments[i].VirtualEnd;
                    break;
                }
            }
        }
    }

    _playbackEngine?.Seek(targetTime);
    _timelineViewModel.CurrentTime = targetTime;
}
```

**Step 2: Add Skip10SecondsForward command**

Add after Skip10SecondsBackward:

```csharp
[RelayCommand]
private void Skip10SecondsForward()
{
    if (_playbackEngine?.CurrentPosition == null || _timelineViewModel?.Metrics == null) return;

    var currentTime = _playbackEngine.CurrentPosition.Value;
    var targetTime = currentTime + TimeSpan.FromSeconds(10);
    var maxDuration = _timelineViewModel.Metrics.TotalDuration;

    // Clamp to valid range
    if (targetTime > maxDuration)
        targetTime = maxDuration;

    // If we have segment manager, ensure we land on a visible frame
    if (_segmentManager != null)
    {
        var sourceTime = _timelineViewModel.VirtualToSourceTime(targetTime);
        var virtualTime = _timelineViewModel.SourceToVirtualTime(sourceTime);

        // If target is in deleted region, find next visible frame
        if (virtualTime == null)
        {
            var segments = _segmentManager.CurrentSegments.KeptSegments;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].SourceStart >= sourceTime)
                {
                    targetTime = segments[i].VirtualStart;
                    break;
                }
            }
        }
    }

    _playbackEngine?.Seek(targetTime);
    _timelineViewModel.CurrentTime = targetTime;
}
```

**Step 3: Update Stop command for segment awareness**

Find the existing Stop command and modify it:

```csharp
[RelayCommand]
private void Stop()
{
    _playbackEngine?.Stop();

    // Find first visible frame
    var firstVisibleTime = TimeSpan.Zero;

    if (_segmentManager != null)
    {
        var segments = _segmentManager.CurrentSegments.KeptSegments;
        if (segments.Count > 0)
        {
            // If start is deleted, use first segment's start
            firstVisibleTime = segments[0].VirtualStart;
        }
    }

    _timelineViewModel.CurrentTime = firstVisibleTime;
    _playbackEngine?.Seek(firstVisibleTime);
}
```

**Step 4: Build and verify no compilation errors**

Run: `./src/scripts/build-macos.sh`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs
git commit -m "feat: add segment-aware skip and stop commands"
```

---

## Task 2: Create PlayerControlsOverlay UserControl

**Files:**
- Create: `src/SpartaCut/Controls/PlayerControlsOverlay.axaml`
- Create: `src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs`

**Step 1: Create XAML file with control structure**

Create `src/SpartaCut/Controls/PlayerControlsOverlay.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:SpartaCut.Core.ViewModels"
             x:Class="SpartaCut.Controls.PlayerControlsOverlay"
             x:DataType="vm:MainWindowViewModel">

    <!-- Semi-transparent dark overlay at bottom -->
    <Border VerticalAlignment="Bottom"
            Height="64"
            Background="#CC000000"
            Name="ControlsContainer"
            Opacity="1">

        <Grid Margin="16,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/> <!-- Play/Pause -->
                <ColumnDefinition Width="Auto"/> <!-- Stop -->
                <ColumnDefinition Width="Auto"/> <!-- Skip Back -->
                <ColumnDefinition Width="Auto"/> <!-- Skip Forward -->
                <ColumnDefinition Width="*"/>    <!-- Spacer -->
                <ColumnDefinition Width="Auto"/> <!-- Timecode -->
                <ColumnDefinition Width="Auto"/> <!-- Mute -->
                <ColumnDefinition Width="Auto"/> <!-- Volume Slider -->
            </Grid.ColumnDefinitions>

            <!-- Play/Pause Button -->
            <Button Grid.Column="0"
                    Width="48" Height="48"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding PlayCommand}"
                    IsVisible="{Binding !IsPlaying}">
                <TextBlock Text="▶" FontSize="28" Foreground="White"/>
            </Button>

            <Button Grid.Column="0"
                    Width="48" Height="48"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding PauseCommand}"
                    IsVisible="{Binding IsPlaying}">
                <TextBlock Text="⏸" FontSize="28" Foreground="White"/>
            </Button>

            <!-- Stop Button -->
            <Button Grid.Column="1"
                    Width="48" Height="48"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding StopCommand}">
                <TextBlock Text="⏹" FontSize="28" Foreground="White"/>
            </Button>

            <!-- Skip Back 10s -->
            <Button Grid.Column="2"
                    Width="48" Height="48"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding Skip10SecondsBackwardCommand}">
                <TextBlock Text="⏪" FontSize="28" Foreground="White"/>
            </Button>

            <!-- Skip Forward 10s -->
            <Button Grid.Column="3"
                    Width="48" Height="48"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding Skip10SecondsForwardCommand}">
                <TextBlock Text="⏩" FontSize="28" Foreground="White"/>
            </Button>

            <!-- Timecode Display -->
            <TextBlock Grid.Column="5"
                       Text="{Binding CurrentTimeDisplay}"
                       FontFamily="Consolas,Courier New,monospace"
                       FontSize="14"
                       Foreground="White"
                       VerticalAlignment="Center"
                       Margin="0,0,16,0"/>

            <!-- Mute Button -->
            <Button Grid.Column="6"
                    Width="40" Height="40"
                    Margin="0,0,8,0"
                    Background="Transparent"
                    BorderThickness="0"
                    Command="{Binding ToggleMuteCommand}">
                <TextBlock Text="{Binding IsMuted, Converter={StaticResource MuteIconConverter}}"
                           FontSize="20" Foreground="White"/>
            </Button>

            <!-- Volume Slider -->
            <Slider Grid.Column="7"
                    Width="120"
                    Minimum="0"
                    Maximum="1"
                    Value="{Binding Volume, Mode=TwoWay}"
                    VerticalAlignment="Center"/>
        </Grid>
    </Border>
</UserControl>
```

**Step 2: Create code-behind with fade logic**

Create `src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs`:

```csharp
using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SpartaCut.Controls;

public partial class PlayerControlsOverlay : UserControl
{
    private DispatcherTimer? _fadeTimer;
    private const double IDLE_OPACITY = 0.5;
    private const double ACTIVE_OPACITY = 1.0;
    private const int FADE_DELAY_MS = 2000;

    public PlayerControlsOverlay()
    {
        InitializeComponent();

        // Setup fade timer
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(FADE_DELAY_MS)
        };
        _fadeTimer.Tick += OnFadeTimerTick;

        // Track mouse movement
        this.PointerMoved += OnPointerMoved;
        this.PointerEntered += OnPointerEntered;
        this.PointerExited += OnPointerExited;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Reset fade timer on mouse movement
        SetActiveOpacity();
        _fadeTimer?.Stop();
        _fadeTimer?.Start();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        SetActiveOpacity();
        _fadeTimer?.Stop();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _fadeTimer?.Start();
    }

    private void OnFadeTimerTick(object? sender, EventArgs e)
    {
        _fadeTimer?.Stop();
        SetIdleOpacity();
    }

    private void SetActiveOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, ACTIVE_OPACITY);
        }
    }

    private void SetIdleOpacity()
    {
        if (this.FindControl<Border>("ControlsContainer") is Border container)
        {
            AnimateOpacity(container, IDLE_OPACITY);
        }
    }

    private void AnimateOpacity(Border target, double toOpacity)
    {
        var animation = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters = { new Setter(OpacityProperty, target.Opacity) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters = { new Setter(OpacityProperty, toOpacity) }
                }
            }
        };

        animation.RunAsync(target);
    }
}
```

**Step 3: Build and verify compilation**

Run: `./src/scripts/build-macos.sh`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/SpartaCut/Controls/PlayerControlsOverlay.axaml src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs
git commit -m "feat: create PlayerControlsOverlay with fade behavior"
```

---

## Task 3: Add CurrentTimeDisplay Property to ViewModel

**Files:**
- Modify: `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`

**Step 1: Add CurrentTimeDisplay observable property**

Add after existing observable properties:

```csharp
[ObservableProperty]
private string _currentTimeDisplay = "00:00:00";
```

**Step 2: Update CurrentTimeDisplay when playback position changes**

Find the existing playback position update logic (likely in a timer or event handler) and add:

```csharp
private void UpdateCurrentTimeDisplay()
{
    if (_playbackEngine?.CurrentPosition != null)
    {
        var time = _playbackEngine.CurrentPosition.Value;
        CurrentTimeDisplay = time.ToString(@"hh\:mm\:ss");
    }
    else
    {
        CurrentTimeDisplay = "00:00:00";
    }
}
```

Call this method wherever playback position is updated (search for `_playbackEngine.CurrentPosition`).

**Step 3: Add ToggleMute command**

Add after volume-related code:

```csharp
private bool _isMuted = false;
private double _volumeBeforeMute = 1.0;

[ObservableProperty]
private bool _isMutedProperty;

[RelayCommand]
private void ToggleMute()
{
    if (_isMuted)
    {
        // Unmute: restore previous volume
        Volume = _volumeBeforeMute;
        _isMuted = false;
    }
    else
    {
        // Mute: save current volume and set to 0
        _volumeBeforeMute = Volume;
        Volume = 0;
        _isMuted = true;
    }

    IsMutedProperty = _isMuted;
}
```

**Step 4: Build and verify**

Run: `./src/scripts/build-macos.sh`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs
git commit -m "feat: add CurrentTimeDisplay and ToggleMute command"
```

---

## Task 4: Create Mute Icon Converter

**Files:**
- Create: `src/SpartaCut/Converters/MuteIconConverter.cs`

**Step 1: Create converter class**

```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SpartaCut.Converters;

public class MuteIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isMuted)
        {
            return isMuted ? "🔇" : "🔊";
        }
        return "🔊";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

**Step 2: Register converter in App.axaml**

Modify `src/SpartaCut/App.axaml`, add to Application.Resources:

```xml
<Application.Resources>
    <local:MuteIconConverter x:Key="MuteIconConverter"/>
</Application.Resources>
```

Add xmlns:local at top if not present:

```xml
xmlns:local="using:SpartaCut.Converters"
```

**Step 3: Build and verify**

Run: `./src/scripts/build-macos.sh`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/SpartaCut/Converters/MuteIconConverter.cs src/SpartaCut/App.axaml
git commit -m "feat: add mute icon converter for overlay controls"
```

---

## Task 5: Integrate Overlay into VlcPlayerControl

**Files:**
- Modify: `src/SpartaCut/Views/MainWindow.axaml`

**Step 1: Replace VlcPlayerControl with Grid containing player and overlay**

Find the VlcPlayerControl (around line 107):

```xml
<!-- OLD CODE - REMOVE -->
<controls:VlcPlayerControl x:Name="VlcPlayer"
                           Grid.Row="2"
                           Margin="20,0,20,20" />
```

Replace with:

```xml
<!-- Video Player with Overlay Controls -->
<Grid Grid.Row="2" Margin="20,0,20,20">
    <controls:VlcPlayerControl x:Name="VlcPlayer" />
    <controls:PlayerControlsOverlay DataContext="{Binding}" />
</Grid>
```

**Step 2: Remove old toolbar playback controls**

Find and remove the toolbar (Grid.Row="1"):

```xml
<!-- OLD CODE - REMOVE ENTIRE StackPanel -->
<StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="10" Margin="20,10,20,20">
    <!-- All playback and volume controls -->
</StackPanel>
```

Update Grid.RowDefinitions to remove the toolbar row:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/> <!-- Windows Menu Bar (unchanged) -->
    <RowDefinition Height="*"/>    <!-- Video Player (was row 2, now row 1) -->
    <RowDefinition Height="Auto"/> <!-- Export Progress (was row 3, now row 2) -->
    <RowDefinition Height="Auto"/> <!-- Timeline (was row 4, now row 3) -->
</Grid.RowDefinitions>
```

Update all Grid.Row values after the removed toolbar:
- Video Player: Grid.Row="2" → Grid.Row="1"
- Export Progress: Grid.Row="3" → Grid.Row="2"
- Timeline: Grid.Row="4" → Grid.Row="3"

**Step 3: Build and test**

Run: `./src/scripts/build-macos.sh`
Expected: Build succeeds

Run: `./src/scripts/run-macos.sh`
Expected: Application launches with overlay controls visible on video player

**Step 4: Commit**

```bash
git add src/SpartaCut/Views/MainWindow.axaml
git commit -m "feat: integrate player controls overlay and remove toolbar controls"
```

---

## Task 6: Make Mouse Activity Propagate to Overlay

**Files:**
- Modify: `src/SpartaCut/Views/MainWindow.axaml`

**Step 1: Add pointer event handlers to video player Grid**

Update the Grid containing VlcPlayer:

```xml
<Grid Grid.Row="1"
      Margin="20,0,20,20"
      PointerMoved="OnPlayerPointerMoved">
    <controls:VlcPlayerControl x:Name="VlcPlayer" />
    <controls:PlayerControlsOverlay x:Name="PlayerControls" DataContext="{Binding}" />
</Grid>
```

**Step 2: Add event handler in code-behind**

Modify `src/SpartaCut/Views/MainWindow.axaml.cs`, add method:

```csharp
private void OnPlayerPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
{
    // Trigger overlay's pointer moved handler to reset fade timer
    if (PlayerControls != null)
    {
        PlayerControls.RaiseEvent(e);
    }
}
```

**Step 3: Update PlayerControlsOverlay to expose public method**

Modify `src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs`, make OnPointerMoved public:

```csharp
public void OnPointerMoved(object? sender, PointerEventArgs e)
{
    // Reset fade timer on mouse movement
    SetActiveOpacity();
    _fadeTimer?.Stop();
    _fadeTimer?.Start();
}
```

Change the event subscription in constructor to:

```csharp
// Don't subscribe to self - will be triggered from parent
// this.PointerMoved += OnPointerMoved; // REMOVE
```

**Step 4: Build and test fade behavior**

Run: `./src/scripts/build-macos.sh`
Run: `./src/scripts/run-macos.sh`

Test:
1. Load a video
2. Move mouse over player - controls should be at full opacity
3. Stop moving mouse for 2 seconds - controls should fade to 50% opacity
4. Move mouse again - controls should return to full opacity

**Step 5: Commit**

```bash
git add src/SpartaCut/Views/MainWindow.axaml src/SpartaCut/Views/MainWindow.axaml.cs src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs
git commit -m "feat: implement mouse activity detection for overlay fade"
```

---

## Task 7: Fix Animation and Final Polish

**Files:**
- Modify: `src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs`

**Step 1: Simplify opacity animation using Transitions**

Replace the AnimateOpacity method with direct property set + CSS-style transitions.

In XAML (`PlayerControlsOverlay.axaml`), add Transitions to Border:

```xml
<Border VerticalAlignment="Bottom"
        Height="64"
        Background="#CC000000"
        Name="ControlsContainer"
        Opacity="1">
    <Border.Transitions>
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.3"/>
        </Transitions>
    </Border.Transitions>
    <!-- ... rest of content ... -->
</Border>
```

In code-behind, simplify AnimateOpacity:

```csharp
private void AnimateOpacity(Border target, double toOpacity)
{
    target.Opacity = toOpacity;
}
```

**Step 2: Test all controls functionality**

Run: `./src/scripts/build-macos.sh`
Run: `./src/scripts/run-macos.sh`

Test checklist:
- [ ] Play/Pause toggle works
- [ ] Stop goes to first visible frame
- [ ] Skip back 10s works and respects segments
- [ ] Skip forward 10s works and respects segments
- [ ] Volume slider controls playback volume
- [ ] Mute button toggles mute state and icon changes
- [ ] Timecode displays current playback position
- [ ] Overlay fades to 50% opacity after 2s idle
- [ ] Overlay returns to 100% opacity on mouse movement
- [ ] Keyboard shortcuts still work (Space, M, etc.)

**Step 3: Commit**

```bash
git add src/SpartaCut/Controls/PlayerControlsOverlay.axaml src/SpartaCut/Controls/PlayerControlsOverlay.axaml.cs
git commit -m "feat: finalize player controls overlay with transitions"
```

---

## Verification Steps

After completing all tasks:

1. **Build**: `./src/scripts/build-macos.sh` - no errors
2. **Run**: `./src/scripts/run-macos.sh`
3. **Load video** and verify:
   - Controls visible at bottom of player
   - All buttons functional
   - Fade behavior works
   - Segment-aware playback works
   - Keyboard shortcuts still work
4. **Create final commit** if any fixes needed
5. **Push**: `git push origin dev`

---

## Notes for Engineer

- **Segment awareness**: The SegmentManager tracks deleted regions. Use `VirtualToSourceTime` and `SourceToVirtualTime` to convert between timeline coordinates
- **Existing patterns**: Follow button state pattern from CLAUDE.md (observable properties + IsEnabled, NOT CanExecute)
- **Font**: Use system fonts for icons (▶⏸⏹⏪⏩🔊🔇) - they work cross-platform
- **Testing**: Manual testing only for UI - no automated tests needed for overlay behavior
- **Commit frequency**: After each task as specified above
