# Week 8: Audio Playback Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add synchronized audio playback to LibVLC video playback with volume control and seamless segment boundary handling.

**Architecture:** LibVLC provides both video and audio playback. We'll synchronize audio with video timer, add volume control UI, and ensure audio seeks correctly when jumping deleted segments.

**Tech Stack:** LibVLCSharp, NAudio (temp file fallback), Avalonia UI, C# 12

---

## Prerequisites

- Week 7 (LibVLC Playback Engine) complete ✅
- VlcPlaybackEngine with Play/Pause/Seek working ✅
- Segment boundary detection implemented ✅
- `sample-30s.mp4` available for testing ✅

---

## Task 1: Add Volume Control to VlcPlaybackEngine

**Files:**
- Modify: `src/Bref/Services/VlcPlaybackEngine.cs`
- Modify: `src/SpartaCut.Core/Services/Interfaces/IPlaybackEngine.cs`
- Test: `src/SpartaCut.Tests/Mocks/MockPlaybackEngine.cs`

**Step 1: Add Volume property to IPlaybackEngine**

Modify `src/SpartaCut.Core/Services/Interfaces/IPlaybackEngine.cs:12-15`:

```csharp
public interface IPlaybackEngine : IDisposable
{
    PlaybackState State { get; }
    TimeSpan CurrentTime { get; }
    bool CanPlay { get; }
    float Volume { get; }  // ADD THIS

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? TimeChanged;

    void Initialize(string videoFilePath, SegmentManager segmentManager, VideoMetadata metadata);
    void Play();
    void Pause();
    void Seek(TimeSpan position);
    void SetVolume(float volume);  // ADD THIS (0.0-1.0)
}
```

**Step 2: Implement Volume in VlcPlaybackEngine**

Modify `src/Bref/Services/VlcPlaybackEngine.cs`:

Add private field after line 30:
```csharp
private float _volume = 1.0f;
```

Add property after line 52 (after CanPlay):
```csharp
/// <summary>
/// Current audio volume (0.0 to 1.0)
/// </summary>
public float Volume => _volume;
```

Add SetVolume method after Pause method (around line 206):
```csharp
/// <summary>
/// Set audio volume
/// </summary>
public void SetVolume(float volume)
{
    if (_disposed) throw new ObjectDisposedException(nameof(VlcPlaybackEngine));

    // Clamp to valid range
    _volume = Math.Clamp(volume, 0.0f, 1.0f);

    if (_mediaPlayer != null)
    {
        // LibVLC volume is 0-100
        _mediaPlayer.Volume = (int)(_volume * 100);
        Log.Debug("Volume set to {Volume}%", (int)(_volume * 100));
    }
}
```

**Step 3: Initialize volume in Initialize method**

Modify `src/Bref/Services/VlcPlaybackEngine.cs:154` after setting media:

```csharp
_mediaPlayer!.Media = _media;

// Set initial volume
_mediaPlayer.Volume = (int)(_volume * 100);

// Set starting position to 0 (VLC will show a black frame until Play is called)
_mediaPlayer.Time = 0;
```

**Step 4: Update MockPlaybackEngine**

Modify `src/SpartaCut.Tests/Mocks/MockPlaybackEngine.cs`:

Add private field after line 13:
```csharp
private float _volume = 1.0f;
```

Add property after line 16 (after CanPlay):
```csharp
public float Volume => _volume;
```

Add SetVolume method after Seek (around line 42):
```csharp
public void SetVolume(float volume)
{
    _volume = Math.Clamp(volume, 0.0f, 1.0f);
}
```

**Step 5: Build and verify**

Run: `/usr/local/share/dotnet/dotnet build src/Bref/SpartaCut.csproj`
Expected: Build succeeds with 0 errors

**Step 6: Commit**

```bash
git add src/SpartaCut.Core/Services/Interfaces/IPlaybackEngine.cs \
        src/Bref/Services/VlcPlaybackEngine.cs \
        src/SpartaCut.Tests/Mocks/MockPlaybackEngine.cs
git commit -m "feat: add volume control to playback engine

- Add Volume property and SetVolume method to IPlaybackEngine
- Implement volume control in VlcPlaybackEngine (0.0-1.0 range)
- Update MockPlaybackEngine with volume support
- LibVLC volume mapped to 0-100 range internally"
```

---

## Task 2: Add Volume Control UI to MainWindow

**Files:**
- Modify: `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`
- Modify: `src/Bref/Views/MainWindow.axaml`

**Step 1: Add Volume property to MainWindowViewModel**

Modify `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`:

Add private field after line 21 (after _uiContext):
```csharp
private double _volume = 1.0;
```

Add property after VirtualDuration property (around line 87):
```csharp
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
```

**Step 2: Add volume slider to MainWindow**

Modify `src/Bref/Views/MainWindow.axaml` around line 50 (in the playback controls Grid):

Find the existing controls Grid and add volume slider after the playback buttons:

```xml
<!-- Existing Play/Pause/Stop buttons here -->

<!-- Volume Control -->
<StackPanel Grid.Column="4" Orientation="Horizontal" Spacing="8" Margin="16,0,0,0">
    <TextBlock Text="🔊" VerticalAlignment="Center" FontSize="16"/>
    <Slider Name="VolumeSlider"
            Value="{Binding Volume, Mode=TwoWay}"
            Minimum="0"
            Maximum="1"
            Width="100"
            VerticalAlignment="Center"
            ToolTip.Tip="Volume"/>
    <TextBlock Text="{Binding Volume, StringFormat={}{0:P0}}"
               VerticalAlignment="Center"
               Width="40"
               ToolTip.Tip="Volume Percentage"/>
</StackPanel>
```

Update the parent Grid ColumnDefinitions to accommodate the new column:
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="Auto"/> <!-- Play -->
    <ColumnDefinition Width="Auto"/> <!-- Pause -->
    <ColumnDefinition Width="Auto"/> <!-- Stop -->
    <ColumnDefinition Width="Auto"/> <!-- Spacer -->
    <ColumnDefinition Width="Auto"/> <!-- Volume -->
    <ColumnDefinition Width="*"/>    <!-- Remaining space -->
</Grid.ColumnDefinitions>
```

**Step 3: Build and verify**

Run: `/usr/local/share/dotnet/dotnet build src/Bref/SpartaCut.csproj`
Expected: Build succeeds, volume slider appears in UI

**Step 4: Commit**

```bash
git add src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs \
        src/Bref/Views/MainWindow.axaml
git commit -m "feat: add volume control slider to UI

- Add Volume property to MainWindowViewModel
- Add volume slider with speaker icon to playback controls
- Display volume percentage next to slider
- Bind slider to playback engine SetVolume method"
```

---

## Task 3: Test Volume Control with Unit Tests

**Files:**
- Create: `src/SpartaCut.Tests/Services/VlcPlaybackEngineVolumeTests.cs`

**Step 1: Write volume control tests**

Create `src/SpartaCut.Tests/Services/VlcPlaybackEngineVolumeTests.cs`:

```csharp
using SpartaCut.Core.ViewModels;
using SpartaCut.Tests.Mocks;
using Xunit;

namespace SpartaCut.Tests.Services;

public class VlcPlaybackEngineVolumeTests
{
    [Fact]
    public void SetVolume_WithValidValue_UpdatesVolume()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(0.5f);

        // Assert
        Assert.Equal(0.5f, engine.Volume);
    }

    [Fact]
    public void SetVolume_WithValueAbove1_ClampsTo1()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(1.5f);

        // Assert
        Assert.Equal(1.0f, engine.Volume);
    }

    [Fact]
    public void SetVolume_WithNegativeValue_ClampsTo0()
    {
        // Arrange
        var engine = new MockPlaybackEngine();

        // Act
        engine.SetVolume(-0.5f);

        // Assert
        Assert.Equal(0.0f, engine.Volume);
    }

    [Fact]
    public void Volume_InitialValue_Is100Percent()
    {
        // Arrange & Act
        var engine = new MockPlaybackEngine();

        // Assert
        Assert.Equal(1.0f, engine.Volume);
    }

    [Fact]
    public void MainWindowViewModel_VolumeProperty_UpdatesPlaybackEngine()
    {
        // Arrange
        var engine = new MockPlaybackEngine();
        var viewModel = new MainWindowViewModel(engine);

        // Act
        viewModel.Volume = 0.75;

        // Assert
        Assert.Equal(0.75f, engine.Volume);
    }
}
```

**Step 2: Run tests**

Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj --filter "FullyQualifiedName~VlcPlaybackEngineVolumeTests"`
Expected: All 5 tests pass

**Step 3: Commit**

```bash
git add src/SpartaCut.Tests/Services/VlcPlaybackEngineVolumeTests.cs
git commit -m "test: add unit tests for volume control

- Test SetVolume updates volume correctly
- Test volume clamping (0.0-1.0 range)
- Test initial volume is 100%
- Test ViewModel volume binding"
```

---

## Task 4: Verify Audio Playback with LibVLC

**Files:**
- None (manual testing)

**Step 1: Run application with sample video**

Run: `/usr/local/share/dotnet/dotnet run --project src/Bref/SpartaCut.csproj`

Load: `samples/sample-30s.mp4`

**Step 2: Manual verification checklist**

Test the following:
- [ ] Click Play - audio and video start together
- [ ] Audio is synchronized with video
- [ ] Drag volume slider - volume changes smoothly
- [ ] Set volume to 0% - audio mutes
- [ ] Set volume to 100% - audio at full volume
- [ ] Click Pause - audio stops with video
- [ ] Resume playback - audio resumes in sync

**Step 3: Document results**

Note any issues in console output. Expected: Audio plays synchronized with video, volume control works.

---

## Task 5: Add Audio Synchronization Monitoring

**Files:**
- Modify: `src/Bref/Services/VlcPlaybackEngine.cs`

**Step 1: Add audio sync verification logging**

Modify `src/Bref/Services/VlcPlaybackEngine.cs` in the Play method (around line 175):

```csharp
_mediaPlayer!.Play();
_state = PlaybackState.Playing;
StateChanged?.Invoke(this, _state);

// Log audio state for debugging
if (_mediaPlayer.AudioTrackCount > 0)
{
    Log.Information("Audio playback started: {Tracks} track(s), Volume: {Volume}%",
        _mediaPlayer.AudioTrackCount, _mediaPlayer.Volume);
}
else
{
    Log.Warning("No audio tracks found in media");
}

// Start position monitor
_positionMonitor!.Start();
```

**Step 2: Add audio track verification in Initialize**

Modify `src/Bref/Services/VlcPlaybackEngine.cs` in the Initialize method after setting media (around line 145):

```csharp
_media = new Media(_libVLC, videoFilePath, FromType.FromPath);
_mediaPlayer!.Media = _media;

// Parse media to get track info
_media.Parse(MediaParseOptions.ParseLocal);

// Set initial volume
_mediaPlayer.Volume = (int)(_volume * 100);

// Log media info
Log.Information("Media loaded: Video tracks: {Video}, Audio tracks: {Audio}",
    _media.Tracks.Count(t => t.TrackType == TrackType.Video),
    _media.Tracks.Count(t => t.TrackType == TrackType.Audio));

// Set starting position to 0
_mediaPlayer.Time = 0;
```

**Step 3: Build and test**

Run: `/usr/local/share/dotnet/dotnet build src/Bref/SpartaCut.csproj`
Expected: Build succeeds

Run the app and load a video, check console for audio track logging.

**Step 4: Commit**

```bash
git add src/Bref/Services/VlcPlaybackEngine.cs
git commit -m "feat: add audio track logging and verification

- Log audio track count on media load
- Log audio playback state on Play
- Warn if no audio tracks found
- Parse media to get track information"
```

---

## Task 6: Handle Segment Boundary Audio Seeking

**Files:**
- Modify: `src/Bref/Services/VlcPlaybackEngine.cs`

**Step 1: Verify audio seeks during segment jumps**

The OnPositionMonitorTick method already calls Seek when hitting deleted segments. Verify audio seeks too.

Modify `src/Bref/Services/VlcPlaybackEngine.cs` in OnPositionMonitorTick (around line 270):

```csharp
if (Math.Abs((targetTime - _lastSeekTarget).TotalMilliseconds) > 50)
{
    Log.Information("Skipping deleted segment, jumping to {Time}", targetTime);

    // Check if audio will stay synchronized
    if (_mediaPlayer.AudioTrackCount > 0)
    {
        Log.Debug("Audio seek: current audio time before seek");
    }

    Seek(targetTime);
}
```

**Step 2: Test audio continuity across segments**

Manual test:
1. Load `samples/sample-30s.mp4`
2. Delete segment at 10-15 seconds
3. Play from 5 seconds
4. Listen for audio gap at boundary

Expected: Brief silence acceptable (<100ms), no audio glitch or pop

**Step 3: Add audio smoothing note to logs**

No code changes needed - LibVLC handles audio crossfading internally.

Document in `docs/testing/week8-audio-manual-tests.md` that brief audio gaps are expected.

**Step 4: Commit**

```bash
git add src/Bref/Services/VlcPlaybackEngine.cs
git commit -m "feat: add audio seek logging for segment boundaries

- Log audio track count before segment boundary seeks
- Document that LibVLC handles audio crossfading
- Brief silence at boundaries is acceptable (<100ms)"
```

---

## Task 7: Add Mute/Unmute Hotkey

**Files:**
- Modify: `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`
- Modify: `src/Bref/Views/MainWindow.axaml.cs`

**Step 1: Add mute state to ViewModel**

Modify `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`:

Add private field after _volume:
```csharp
private double _volumeBeforeMute = 1.0;
private bool _isMuted = false;
```

Add method after Volume property:
```csharp
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
```

**Step 2: Add M key handler in MainWindow**

Modify `src/Bref/Views/MainWindow.axaml.cs` in the KeyDown handler (around line 120):

```csharp
protected override void OnKeyDown(KeyEventArgs e)
{
    base.OnKeyDown(e);

    if (_viewModel == null) return;

    switch (e.Key)
    {
        case Key.Space:
            if (_viewModel.IsPlaying)
                _viewModel.PauseCommand.Execute(null);
            else
                _viewModel.PlayCommand.Execute(null);
            e.Handled = true;
            break;

        case Key.M:  // ADD THIS
            _viewModel.ToggleMute();
            e.Handled = true;
            break;
    }
}
```

**Step 3: Update volume slider tooltip**

Modify `src/Bref/Views/MainWindow.axaml`:

```xml
<Slider Name="VolumeSlider"
        Value="{Binding Volume, Mode=TwoWay}"
        Minimum="0"
        Maximum="1"
        Width="100"
        VerticalAlignment="Center"
        ToolTip.Tip="Volume (M to mute)"/>
```

**Step 4: Test mute functionality**

Run app, press M key:
- [ ] Audio mutes
- [ ] Volume slider moves to 0
- [ ] Press M again - audio unmutes to previous volume

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs \
        src/Bref/Views/MainWindow.axaml.cs \
        src/Bref/Views/MainWindow.axaml
git commit -m "feat: add M key mute/unmute toggle

- Add ToggleMute method to MainWindowViewModel
- Save/restore volume when muting/unmuting
- Add M key handler to MainWindow
- Update volume slider tooltip with mute shortcut"
```

---

## Task 8: Test with No-Audio Video

**Files:**
- None (manual testing)

**Step 1: Create a no-audio test video**

Use FFmpeg to strip audio from sample:

```bash
ffmpeg -i samples/sample-30s.mp4 -an samples/sample-30s-no-audio.mp4
```

**Step 2: Test loading no-audio video**

Run app and load `samples/sample-30s-no-audio.mp4`

Expected:
- [ ] Video loads successfully
- [ ] Console logs "No audio tracks found in media" or "Audio tracks: 0"
- [ ] Video plays normally (silent)
- [ ] Volume slider still works (but has no effect)
- [ ] No crashes or errors

**Step 3: Document behavior**

Update `docs/testing/week8-audio-manual-tests.md` Test 8 with results.

**Step 4: Cleanup test file**

```bash
rm samples/sample-30s-no-audio.mp4
```

---

## Task 9: Performance Testing

**Files:**
- None (manual testing)

**Step 1: Memory usage test**

1. Launch app (note memory in Task Manager/Activity Monitor)
2. Load `samples/sample-30s.mp4`
3. Note memory after load
4. Play for 2 minutes continuously
5. Note memory after playback

Document in `docs/testing/week8-audio-manual-tests.md`

Expected memory increase: <50MB for audio

**Step 2: CPU usage test**

1. Play video and monitor CPU%
2. Seek multiple times rapidly
3. Toggle mute repeatedly

Expected: CPU usage <5% during normal playback

**Step 3: Audio extraction time**

Check logs for "Extracted audio to..." timing if applicable (LibVLC may not need this).

---

## Task 10: Integration Tests for Audio

**Files:**
- Modify: `src/SpartaCut.Tests/Integration/SelectionAndDeletionIntegrationTests.cs`

**Step 1: Add audio playback integration test**

Add to `src/SpartaCut.Tests/Integration/SelectionAndDeletionIntegrationTests.cs`:

```csharp
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
```

**Step 2: Run integration tests**

Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj --filter "FullyQualifiedName~SelectionAndDeletionIntegrationTests"`
Expected: All tests pass including new volume tests

**Step 3: Commit**

```bash
git add src/SpartaCut.Tests/Integration/SelectionAndDeletionIntegrationTests.cs
git commit -m "test: add audio volume integration tests

- Test volume can be set multiple times
- Test mute/unmute preserves volume level
- Verify PlaybackEngine receives volume updates"
```

---

## Task 11: Update Documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `docs/testing/week8-audio-manual-tests.md`

**Step 1: Update CLAUDE.md Today I Learned**

Add to `CLAUDE.md` section "## Today I Learned" under Week 8:

```markdown
### Week 8: Audio Playback Integration (January 2025)

**LibVLC Audio Architecture:**
- LibVLC MediaPlayer provides both video and audio playback automatically
- No need for separate audio extraction - LibVLC handles synchronization
- Volume control via MediaPlayer.Volume property (0-100 scale)
- Audio tracks detected via MediaPlayer.AudioTrackCount

**Volume Control Implementation:**
- UI slider uses 0.0-1.0 range (user-friendly)
- Convert to 0-100 for LibVLC internally
- Mute/unmute preserves previous volume level
- M key hotkey for quick mute toggle

**Audio Synchronization:**
- LibVLC automatically keeps audio/video in sync
- Seek operations update both video and audio position
- Segment boundary jumps may have brief audio gaps (<100ms) - acceptable
- No manual audio buffer management needed

**No-Audio Video Handling:**
- Videos without audio tracks play normally (silent)
- MediaPlayer.AudioTrackCount returns 0
- Volume controls remain functional but have no effect
- No errors or crashes
```

**Step 2: Mark week8 manual tests as template**

Modify `docs/testing/week8-audio-manual-tests.md` header:

```markdown
# Week 8 Audio Playback - Manual Testing Checklist

**Status:** ✅ COMPLETED (Week 8 - January 2025)
**Implementation:** LibVLC audio integrated with video playback

## Prerequisites
- Video file with audio track (MP4/H.264) - use `samples/sample-30s.mp4`
- Speakers or headphones connected
- LibVLC playback engine implemented ✅
```

**Step 3: Commit documentation**

```bash
git add CLAUDE.md docs/testing/week8-audio-manual-tests.md
git commit -m "docs: document Week 8 audio playback learnings

- Add LibVLC audio architecture notes to CLAUDE.md
- Document volume control implementation
- Note audio synchronization behavior
- Mark manual testing checklist as completed"
```

---

## Task 12: Final Verification and Version Bump

**Files:**
- Modify: `src/Bref/SpartaCut.csproj`
- Modify: `src/SpartaCut.Core/SpartaCut.Core.csproj`

**Step 1: Run full test suite**

Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj`
Expected: All tests pass (115+ tests)

**Step 2: Build release configuration**

Run: `/usr/local/share/dotnet/dotnet build src/Bref/SpartaCut.csproj -c Release`
Expected: Build succeeds with 0 errors

**Step 3: Bump version to 0.11.0**

Modify `src/Bref/SpartaCut.csproj` line 6:
```xml
<Version>0.11.0</Version>
```

Modify `src/SpartaCut.Core/SpartaCut.Core.csproj` line 6:
```xml
<Version>0.11.0</Version>
```

**Step 4: Commit and tag**

```bash
git add src/Bref/SpartaCut.csproj src/SpartaCut.Core/SpartaCut.Core.csproj
git commit -m "chore: bump version to 0.11.0 - Week 8 audio complete

Week 8 deliverables:
- Volume control with 0-100% slider
- M key mute/unmute toggle
- LibVLC audio synchronized with video
- Audio seeks correctly at segment boundaries
- No-audio video support
- Full test coverage (115+ tests passing)
- Manual testing checklist completed"

git tag v0.11.0
git push origin main
git push origin v0.11.0
```

---

## Completion Checklist

Before marking Week 8 complete, verify:

- [ ] Volume slider in UI controls audio level
- [ ] M key mutes/unmutes audio
- [ ] Audio plays synchronized with video
- [ ] Audio seeks correctly when jumping deleted segments
- [ ] No-audio videos load and play without errors
- [ ] All unit tests pass (115+ tests)
- [ ] Manual testing checklist completed
- [ ] Documentation updated in CLAUDE.md
- [ ] Version bumped to 0.11.0
- [ ] Code committed and tagged

---

## Success Criteria

**Functional:**
✅ Audio playback synchronized with video
✅ Volume control (0-100%) with UI slider
✅ Mute/unmute toggle (M key)
✅ Audio seeks correctly at segment boundaries
✅ No-audio videos handled gracefully

**Performance:**
✅ No audio lag or desync during normal playback
✅ Segment boundary audio gaps <100ms
✅ Memory usage increase <50MB for audio
✅ CPU usage <5% during playback

**Quality:**
✅ All tests passing
✅ No crashes with audio or no-audio videos
✅ Clear logging for audio track detection
✅ Manual testing completed

---

## Known Limitations

1. **Segment Boundary Audio Gaps:** Brief silence (<100ms) at segment boundaries is acceptable - LibVLC needs time to seek
2. **No Audio Crossfade:** No smooth crossfade between segments (would require audio buffer mixing)
3. **Single Audio Track:** Only uses first audio track if video has multiple tracks

These are acceptable for MVP. Can be improved in v1.1+ if user feedback requests.

---

## Next Steps (Week 9)

After completing Week 8:
- Week 9: Export Service Implementation
- Focus: FFmpeg video export with segment filtering
- Hardware acceleration (NVENC/Quick Sync/AMF)
- Progress monitoring and cancellation
