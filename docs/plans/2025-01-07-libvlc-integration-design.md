# LibVLCSharp Integration Design

**Date:** 2025-01-07
**Status:** Approved for Implementation
**Branch:** `libvlc`

## Executive Summary

Replace the custom FFmpeg-based video player (FrameDecoder, FrameCache, PlaybackEngine, VideoPlayerControl) with LibVLCSharp for all dynamic video rendering. Keep FFmpeg for static analysis (thumbnails, waveform generation). This addresses critical issues with playback smoothness and audio synchronization while simplifying the codebase.

**Key Decision:** Use LibVLCSharp for continuous playback AND timeline scrubbing, keeping FFmpeg only for thumbnail/waveform generation during video load.

---

## Problem Statement

The current custom FFmpeg-based video player has multiple issues blocking progress:

1. **Playback smoothness** (Priority 1) - Frame-by-frame decoding with timer causes stuttering
2. **Audio synchronization** (Priority 2) - Complex to synchronize NAudio with video frames
3. **Segment boundary handling** (Priority 3) - Need seamless skipping of deleted segments
4. **Development velocity** (Priority 4) - Too much low-level FFmpeg code to maintain

LibVLCSharp addresses #1 and #2 directly (VLC's core strengths), simplifies #4, and provides a workable solution for #3.

---

## Architecture Overview

### High-Level Component Split

**FFmpeg Layer (Static Analysis - Run Once on Load)**
- `VideoService`: Format validation (MP4/H.264)
- `ThumbnailGenerator`: Extract thumbnails at 5-second intervals for timeline
- `WaveformGenerator`: Generate audio waveform data

**LibVLCSharp Layer (Dynamic Rendering - Runtime)**
- `VlcPlayerControl`: Avalonia control wrapping LibVLCSharp.Avalonia.VideoView
- `VlcPlaybackEngine`: Orchestrates playback, monitors position, handles segment boundaries

**Existing Core (Unchanged)**
- `SegmentManager`: Virtual timeline logic (single source of truth)
- `TimelineControl`: Timeline UI with waveform and thumbnails
- `MainWindowViewModel`: Orchestrates all components

### Components to Remove

- ❌ `FrameDecoder` - FFmpeg frame extraction
- ❌ `FrameCache` - LRU cache for decoded frames
- ❌ `VideoPlayerControl` - Custom SkiaSharp rendering
- ❌ `PlaybackEngine` - Timer-based frame updates
- ❌ `LRUCache` - Generic cache utility
- ❌ `VideoFrame` - Frame data model
- ❌ `AudioPlayer` - NAudio stub (VLC handles audio)

---

## Component Specifications

### VlcPlaybackEngine

**Purpose:** Thin orchestrator between LibVLC MediaPlayer and SegmentManager.

**Key Properties:**
```csharp
private LibVLC _libVLC;
private MediaPlayer _mediaPlayer;
private Timer _positionMonitor;  // 50-100ms polling
private SegmentManager _segmentManager;
private bool _isSeeking;  // Prevent seek loops
private TimeSpan _lastSeekTime;  // Throttle scrubbing seeks
```

**Public Interface:**
```csharp
public void Initialize(string videoPath, SegmentManager segmentManager)
public void Play()
public void Pause()
public void Seek(TimeSpan position)
public TimeSpan CurrentTime { get; }
public PlaybackState State { get; }

public event EventHandler<TimeSpan>? TimeChanged;
public event EventHandler<PlaybackState>? StateChanged;
```

**Key Behaviors:**

1. **Position Monitoring (50-100ms polling):**
   - Timer reads `_mediaPlayer.Time`
   - Asks SegmentManager: `SourceToVirtualTime(currentTime)`
   - If returns `null` (deleted segment): find next kept segment, seek immediately
   - Raises `TimeChanged` event for timeline updates

2. **Seek Throttling:**
   - During scrubbing: only process seeks every 50-100ms
   - Queue latest seek position if user scrubs faster
   - Prevents overwhelming VLC with rapid seek commands

3. **Seek Loop Prevention:**
   - Set `_isSeeking = true` before calling `MediaPlayer.Time = ...`
   - Position monitor skips checks while flag is true
   - Clear flag 50ms after seek completes

### VlcPlayerControl

**Purpose:** Thin Avalonia wrapper around LibVLCSharp VideoView.

**XAML:**
```xml
<UserControl xmlns:vlc="clr-namespace:LibVLCSharp.Avalonia;assembly=LibVLCSharp.Avalonia">
    <vlc:VideoView x:Name="VideoView" />
</UserControl>
```

**Code-behind:**
```csharp
public void Initialize(MediaPlayer mediaPlayer)
{
    VideoView.MediaPlayer = mediaPlayer;
}
```

No custom rendering needed - LibVLCSharp handles everything.

---

## Data Flow

### Video Load Flow

1. User selects MP4 file
2. `VideoService.LoadVideoAsync()` validates H.264 format
3. **Parallel execution:**
   - FFmpeg: `ThumbnailGenerator.GenerateAsync()` → thumbnails
   - FFmpeg: `WaveformGenerator.GenerateAsync()` → waveform data
   - LibVLC: `VlcPlaybackEngine.Initialize()` → video ready
4. `SegmentManager.Initialize(duration)` → single segment (full video)
5. Timeline displays with thumbnails/waveform, VLC ready to play

### Continuous Playback Flow

1. User clicks Play (or Space)
2. `MainWindowViewModel.PlayCommand` → `VlcPlaybackEngine.Play()`
3. VLC plays video with synchronized audio
4. Position monitor fires every 50-100ms:
   ```
   currentTime = _mediaPlayer.Time
   virtualTime = _segmentManager.SourceToVirtualTime(currentTime)

   if (virtualTime == null) {
       // In deleted segment - find next kept segment
       nextSegment = FindNextKeptSegment(currentTime)
       if (nextSegment != null) {
           Seek(nextSegment.SourceStart)  // Immediate seek
       } else {
           Pause()  // End of video
       }
   }

   TimeChanged?.Invoke(this, currentTime)
   ```
5. Timeline playhead updates via `TimeChanged` event
6. Continues until pause or end

### Timeline Scrubbing Flow

1. User drags timeline scrubber
2. `TimelineControl.CurrentTime` updates on mouse move
3. `TimelineViewModel.CurrentTimeChanged` event fires
4. `MainWindowViewModel` receives event
5. **Throttling check:** Only seek if >50ms since last seek
6. `VlcPlaybackEngine.Seek(virtualTime)`
7. Convert virtual → source time via SegmentManager
8. `_mediaPlayer.Time = sourceTime` (VLC seeks and renders)

### Segment Deletion Flow (During Playback)

1. User deletes segment while video is playing
2. `SegmentManager.DeleteSegment()` updates kept segments
3. Next position monitor tick (50-100ms later)
4. Detects current position now in deleted region
5. Automatically seeks to next kept segment
6. Playback continues seamlessly

---

## Error Handling

### Fail Fast Strategy

**LibVLC Initialization Failure:**
```csharp
try {
    _libVLC = new LibVLC();
} catch (Exception ex) {
    Log.Error("LibVLC initialization failed", ex);
    throw new InvalidOperationException(
        "Video playback unavailable. LibVLC could not initialize.", ex);
}
```
→ Show error dialog: *"Video playback is unavailable. Please reinstall the application."*

**Video Load Failure:**
```csharp
var media = new Media(_libVLC, videoPath);
_mediaPlayer.Play(media);

// Wait for state change
await Task.Delay(500);

if (_mediaPlayer.State == VLCState.Error) {
    throw new InvalidOperationException(
        "VLC cannot play this video. The file may be corrupted or in an unsupported format.");
}
```
→ Show error dialog: *"Cannot play this video. Try converting to standard H.264 format with HandBrake or VLC."*

**Note:** Since `VideoService` already validates MP4/H.264 format, VLC failures should be rare. If they occur, it's a genuine problem worth surfacing immediately.

---

## Edge Cases

### Seek Loop Prevention

**Problem:** Position monitor detects deleted segment → seeks → monitor fires again mid-seek → seeks again → infinite loop

**Solution:**
```csharp
private bool _isSeeking = false;

public void Seek(TimeSpan position) {
    _isSeeking = true;
    _mediaPlayer.Time = (long)position.TotalMilliseconds;

    // Wait for seek to settle
    Task.Delay(50).ContinueWith(_ => _isSeeking = false);
}

private void OnPositionMonitorTick() {
    if (_isSeeking) return;  // Skip checks during seeks

    // Normal boundary detection logic...
}
```

### Last Segment Ending

**Scenario:** Playback reaches end of last kept segment, no more segments after.

**Solution:**
```csharp
if (virtualTime == null) {
    var nextSegment = _segmentManager.CurrentSegments.KeptSegments
        .FirstOrDefault(s => s.SourceStart > currentTime);

    if (nextSegment == null) {
        // No more kept segments - end of video
        Pause();
        StateChanged?.Invoke(this, PlaybackState.Stopped);
        Log.Information("Playback reached end");
    } else {
        Seek(nextSegment.SourceStart);
    }
}
```

### Rapid Scrubbing

**Problem:** User drags timeline quickly → 60 mouse move events/second → 60 VLC seeks/second → choppy

**Solution:**
```csharp
private DateTime _lastSeekTime = DateTime.MinValue;
private const int SeekThrottleMs = 50;

public void Seek(TimeSpan position) {
    var now = DateTime.UtcNow;
    var timeSinceLastSeek = (now - _lastSeekTime).TotalMilliseconds;

    if (timeSinceLastSeek < SeekThrottleMs) {
        // Too soon - skip this seek (or queue it)
        return;
    }

    _lastSeekTime = now;
    _isSeeking = true;
    _mediaPlayer.Time = (long)position.TotalMilliseconds;

    Task.Delay(50).ContinueWith(_ => _isSeeking = false);
}
```

Target: 15-30fps scrubbing (seek every 50-100ms), which is responsive enough for UX.

---

## Testing Strategy

### Unit Tests (Critical Logic)

Test segment boundary detection logic independently:

**File:** `src/Bref.Tests/Services/SegmentBoundaryTests.cs`

```csharp
public class SegmentBoundaryTests
{
    [Fact]
    public void SourceToVirtualTime_WhenInDeletedSegment_ReturnsNull()
    {
        // Given
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromMinutes(10));
        segmentManager.DeleteSegment(
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(3));

        // When
        var virtualTime = segmentManager.CurrentSegments
            .SourceToVirtualTime(TimeSpan.FromMinutes(2.5));

        // Then
        Assert.Null(virtualTime);  // 2:30 is in deleted 2:00-3:00 region
    }

    [Fact]
    public void FindNextKeptSegment_AfterDeletedRegion_ReturnsCorrectStart()
    {
        // Given
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromMinutes(10));
        segmentManager.DeleteSegment(
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(3));

        // When - find next segment after 2:30
        var nextSegment = segmentManager.CurrentSegments.KeptSegments
            .FirstOrDefault(s => s.SourceStart > TimeSpan.FromMinutes(2.5));

        // Then
        Assert.NotNull(nextSegment);
        Assert.Equal(TimeSpan.FromMinutes(3), nextSegment.SourceStart);
    }

    [Fact]
    public void MultipleDeletedSegments_BoundaryDetection_WorksCorrectly()
    {
        // Given
        var segmentManager = new SegmentManager();
        segmentManager.Initialize(TimeSpan.FromMinutes(10));
        segmentManager.DeleteSegment(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        segmentManager.DeleteSegment(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(6));

        // Then - verify correct segments
        Assert.Equal(3, segmentManager.CurrentSegments.KeptSegments.Count);
        // Segment 1: 0:00-1:00
        // Segment 2: 2:00-5:00
        // Segment 3: 6:00-10:00
    }
}
```

These reuse existing `SegmentManager` tests - just verify boundary detection works.

### Integration Tests (Happy Path)

**File:** `src/Bref.Tests/Integration/VlcPlaybackIntegrationTests.cs`

```csharp
[Fact(Skip = "Requires LibVLC and test video file")]
public async Task VlcPlayback_WithDeletions_SkipsSegments()
{
    // Requires: test-video.mp4 in test resources
    var engine = new VlcPlaybackEngine();
    var segmentManager = new SegmentManager();
    segmentManager.Initialize(TimeSpan.FromMinutes(5));
    segmentManager.DeleteSegment(
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2));

    engine.Initialize("test-video.mp4", segmentManager);
    engine.Play();

    await Task.Delay(3000);  // Play for 3 seconds
    engine.Pause();

    // Should have skipped 1:00-2:00 segment
    Assert.True(engine.CurrentTime > TimeSpan.FromMinutes(2));
}
```

**Note:** Integration tests are skipped by default (require LibVLC installed). Run manually during development.

### Manual Testing Checklist

**File:** `docs/testing/libvlc-integration-manual-tests.md`

| Scenario | Status | Notes |
|----------|--------|-------|
| Load MP4 video | ⬜ | Thumbnails and waveform appear |
| Click Play | ⬜ | Video plays smoothly with audio |
| Drag timeline scrubber | ⬜ | Video updates at 15-30fps |
| Delete segment, play across boundary | ⬜ | Seamless skip (brief flicker OK) |
| Multiple deletions | ⬜ | All boundaries skipped correctly |
| Seek during playback | ⬜ | Video jumps to new position |
| Pause/Resume | ⬜ | Works correctly |
| Play to end | ⬜ | Stops at final frame |
| Undo during playback | ⬜ | Segment restored, playback continues |
| Frame rate accuracy | ⬜ | 10-second playback ≈ 10 seconds ±0.2s |

**Test Videos:**
- Short video (1-2 min): Quick iteration testing
- Medium video (10-30 min): Typical use case (Teams meeting)
- Long video (1+ hour): Performance/memory testing

---

## Performance Characteristics

### Position Monitoring

**Polling Frequency:** 50-100ms (10-20 times per second)

**CPU Impact:** Minimal - simple timestamp comparison and null check
**Boundary Detection Precision:** 2-3 frames overshoot at 30fps (barely perceptible)

### Scrubbing Performance

**Seek Throttling:** 50-100ms (max 10-20 seeks/second)

**Expected Frame Rate:** 15-30fps during scrubbing (acceptable UX)
**Comparison:** Current FFmpeg FrameCache targets 60fps (often misses), LibVLC at 20fps is more reliable

### Memory Footprint

**Before (FFmpeg):**
- FrameCache: ~370MB (60 frames × 1080p RGB24)
- Total: ~500MB typical

**After (LibVLC):**
- VLC internal buffers: ~50-100MB (managed by VLC)
- Total: ~200-300MB typical

**Net savings:** ~200MB less memory usage

---

## Trade-offs Analysis

### Gains

✅ **Playback smoothness** - VLC's optimized decoding pipeline
✅ **Audio synchronization** - Built-in, no manual sync needed
✅ **Less code to maintain** - Remove 1500+ lines (FrameDecoder, FrameCache, custom rendering)
✅ **Development velocity** - Focus on features, not low-level decoding
✅ **Hardware acceleration** - VLC handles GPU decoding automatically

### Losses/Costs

⚠️ **Larger dependency** - ~50-100MB LibVLC binaries (vs ~50MB FFmpeg)
⚠️ **Less control** - VLC is a black box, can't inspect internal state
⚠️ **Segment boundary hiccups** - Brief visual flicker during seeks (acceptable for MVP)
⚠️ **Scrubbing frame rate** - 15-30fps vs 60fps target (acceptable trade-off)

### Acceptable Trade-offs

- Brief flicker at segment boundaries: Yes (users editing out boring content won't mind)
- 50-100MB dependency: Yes (Microsoft Store distribution handles this)
- Lower scrubbing frame rate: Yes (15-30fps is responsive enough)
- Less fine-grained control: Yes (VLC is proven reliable for playback)

### Licensing

- **LibVLCSharp:** LGPL 2.1 (dynamic linking OK for proprietary apps)
- **VLC (LibVLC):** GPL 2.0 (dynamic linking OK, must distribute VLC license)
- **Impact:** No code contribution required, must include VLC license file in distribution

---

## Implementation Plan

### Phase 1: Setup (2-3 hours)

**Tasks:**
1. Add NuGet packages:
   - `LibVLCSharp` (core library)
   - `LibVLCSharp.Avalonia` (Avalonia integration)
   - `VideoLAN.LibVLC.Mac` (macOS build) or `VideoLAN.LibVLC.Windows` (Windows build)

2. Initialize LibVLC and verify it works:
   ```csharp
   var libVLC = new LibVLC();
   var mediaPlayer = new MediaPlayer(libVLC);
   var media = new Media(libVLC, "test-video.mp4");
   mediaPlayer.Play(media);
   ```

3. Create basic `VlcPlayerControl.axaml`:
   ```xml
   <UserControl xmlns:vlc="...">
       <vlc:VideoView x:Name="VideoView" />
   </UserControl>
   ```

4. Verify video renders in Avalonia window

**Completion Criteria:** Can play a test video in LibVLC VideoView control

### Phase 2: Core Integration (4-6 hours)

**Tasks:**
1. Implement `VlcPlaybackEngine`:
   - Constructor: Initialize LibVLC, MediaPlayer
   - `Initialize()`: Load media from file path
   - `Play()`, `Pause()`, `Seek()` methods
   - Wire up events (TimeChanged, StateChanged)

2. Add position monitor timer:
   ```csharp
   _positionMonitor = new Timer(50);  // 50ms = 20Hz
   _positionMonitor.Elapsed += OnPositionMonitorTick;
   ```

3. Implement boundary detection:
   ```csharp
   private void OnPositionMonitorTick() {
       if (_isSeeking) return;

       var currentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
       var virtualTime = _segmentManager.SourceToVirtualTime(currentTime);

       if (virtualTime == null) {
           // Find and seek to next segment
       }

       TimeChanged?.Invoke(this, currentTime);
   }
   ```

4. Add seek throttling (50-100ms)

5. Add seek loop prevention (`_isSeeking` flag)

**Completion Criteria:** VlcPlaybackEngine can play video, detect boundaries, and seek when hitting deleted segments

### Phase 3: UI Integration (2-3 hours)

**Tasks:**
1. Replace `VideoPlayerControl` with `VlcPlayerControl` in `MainWindow.axaml`

2. Update `MainWindowViewModel`:
   - Replace `PlaybackEngine` with `VlcPlaybackEngine`
   - Update `PlayCommand`, `PauseCommand` bindings
   - Wire `VlcPlaybackEngine.TimeChanged` → `Timeline.CurrentTime`

3. Connect timeline scrubbing:
   - Subscribe to `TimelineViewModel.CurrentTimeChanged`
   - Call `VlcPlaybackEngine.Seek()` with throttling

4. Test Play/Pause/Scrub in UI

**Completion Criteria:** UI fully functional with LibVLC playback

### Phase 4: Cleanup & Testing (1-2 hours)

**Tasks:**
1. Delete old files:
   - `FrameDecoder.cs`
   - `FrameCache.cs`
   - `LRUCache.cs`
   - `VideoFrame.cs`
   - Old `PlaybackEngine.cs`
   - Old `VideoPlayerControl.axaml/.cs`
   - `AudioPlayer.cs` (VLC handles audio)

2. Remove related tests:
   - `FrameDecoderTests.cs`
   - `FrameCacheTests.cs`
   - `LRUCacheTests.cs`
   - Old `PlaybackEngineTests.cs`

3. Add new tests:
   - `SegmentBoundaryTests.cs` (unit tests)
   - `VlcPlaybackIntegrationTests.cs` (integration tests, skipped by default)

4. Run manual testing checklist (all scenarios)

5. Update documentation:
   - Update `CLAUDE.md` "Today I Learned" section
   - Update `docs/development-log.md`

**Completion Criteria:** All old code removed, tests passing, manual tests complete

### Total Estimate: 10-15 hours

---

## Migration Safety Net

**Before Starting:**
- Current `libvlc` branch is experimental space
- `main` branch still has working FFmpeg implementation
- If LibVLC doesn't work: abandon branch, return to `main`

**During Development:**
- Commit after each phase completes
- Test continuously (don't wait until end)
- If blocked: document issue, decide to continue or revert

**After Completion:**
- Run full manual testing checklist
- If successful: merge `libvlc` → `main`, tag version 0.8.0
- If issues: keep debugging or revert to `main`

---

## Success Criteria

**Functional:**
- ✅ Video plays smoothly without stuttering
- ✅ Audio synchronized with video (no drift)
- ✅ Timeline scrubbing responsive (15-30fps)
- ✅ Segment boundaries skipped automatically during playback
- ✅ Play/Pause/Seek controls work correctly
- ✅ No crashes during typical workflow

**Performance:**
- ✅ Position monitoring overhead <5% CPU
- ✅ Boundary detection precision <100ms (2-3 frames)
- ✅ Memory usage <300MB typical
- ✅ Scrubbing seek latency <100ms

**Quality:**
- ✅ No regressions in segment deletion workflow
- ✅ Timeline thumbnails/waveform still work (FFmpeg)
- ✅ Export still works (FFmpeg, unchanged)

---

## Risks & Mitigations

### Risk 1: LibVLC doesn't work on macOS M4

**Likelihood:** Low (VLC officially supports Apple Silicon)
**Impact:** High (blocks development)

**Mitigation:**
- Test LibVLC initialization immediately in Phase 1
- If fails: try Rosetta mode or x64 build
- Fallback: develop on Windows VM, test on Mac later

### Risk 2: Segment boundary skips are too jarring

**Likelihood:** Medium (depends on video content)
**Impact:** Medium (UX concern, not blocker)

**Mitigation:**
- Test with real Teams recordings early
- If too jarring: add brief fade-to-black transition (50ms)
- Acceptable for MVP: users want fast editing over perfect playback

### Risk 3: VLC seek performance is slow

**Likelihood:** Low (VLC seek is well-optimized)
**Impact:** Medium (affects scrubbing UX)

**Mitigation:**
- Test scrubbing performance in Phase 2
- If slow: adjust throttling (100ms instead of 50ms)
- Fallback: implement "seek on mouse up" instead of continuous

### Risk 4: Memory usage increases

**Likelihood:** Low (VLC buffers less than FrameCache)
**Impact:** Low (500MB is acceptable)

**Mitigation:**
- Profile memory usage in Phase 4
- If high: adjust VLC caching settings (`--file-caching=300`)

---

## Future Enhancements (Post-MVP)

**After LibVLC integration is stable:**

1. **Smooth boundary transitions** - Add 50ms fade-to-black at segment boundaries
2. **Variable speed playback** - Use VLC's rate control (0.5x, 2x, etc.)
3. **Frame-perfect boundary seeking** - Increase polling to 30fps (33ms) for tighter boundaries
4. **Audio ducking** - Use VLC audio filters to lower volume at boundaries
5. **Thumbnail caching** - Generate all thumbnails upfront, cache to disk for instant reload

---

## Conclusion

LibVLCSharp integration addresses the most critical issues (playback smoothness, audio sync) while simplifying the codebase. The trade-offs (brief boundary hiccups, larger dependency) are acceptable for MVP.

**Key Design Decisions:**
- ✅ Use LibVLC for all dynamic rendering (playback + scrubbing)
- ✅ Keep FFmpeg for static analysis (thumbnails, waveform)
- ✅ Position monitoring at 50-100ms for boundary detection
- ✅ Fail fast error handling (no fallbacks)
- ✅ Hybrid testing (unit + integration + manual)

**Estimated Effort:** 10-15 hours across 4 phases

**Ready for implementation.**
