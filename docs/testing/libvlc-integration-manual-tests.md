# LibVLC Integration Manual Testing

**Date:** [FILL IN DURING TESTING]
**Tester:** [FILL IN]
**Build:** [COMMIT SHA]

## Test Environment

- OS: macOS [VERSION] / Windows 11
- Video Files Used:
  - Short: [1-2 min video name]
  - Medium: [10-30 min video name]
  - Long: [1+ hour video name]

## Test Scenarios

| # | Scenario | Status | Notes |
|---|----------|--------|-------|
| 1 | Load MP4 video | ⬜ | Thumbnails and waveform appear, VLC initializes |
| 2 | Click Play | ⬜ | Video plays smoothly with audio synchronized |
| 3 | Click Pause | ⬜ | Video pauses, audio stops |
| 4 | Press Space (Play) | ⬜ | Video resumes from pause point |
| 5 | Press Space (Pause) | ⬜ | Video pauses |
| 6 | Drag timeline scrubber slowly | ⬜ | Video updates at 15-30fps, responsive |
| 7 | Drag timeline scrubber quickly | ⬜ | Seeks throttled, no crashes |
| 8 | Delete segment, play across boundary | ⬜ | Brief flicker OK, skips deleted part |
| 9 | Multiple deletions (3+ segments) | ⬜ | All boundaries skipped correctly |
| 10 | Undo deletion during playback | ⬜ | Segment restored, playback continues |
| 11 | Seek during playback | ⬜ | Video jumps to new position smoothly |
| 12 | Play to end of video | ⬜ | Stops at final frame, state = Stopped |
| 13 | Play after reaching end | ⬜ | Starts from beginning |
| 14 | Frame rate accuracy (10 sec) | ⬜ | Playback time ≈ 10s ±0.2s |
| 15 | Memory usage | ⬜ | <300MB during playback |

## Detailed Test Results

### Test 1: Load MP4 Video
- [ ] Video loads without errors
- [ ] Thumbnails appear on timeline
- [ ] Waveform renders correctly
- [ ] First frame displays in video player
- [ ] No console errors

### Test 2-5: Playback Controls
- [ ] Play button works
- [ ] Pause button works
- [ ] Space hotkey toggles play/pause
- [ ] Audio synchronized with video
- [ ] No stuttering or frame drops

### Test 6-7: Timeline Scrubbing
- [ ] Scrubbing updates video frame
- [ ] Scrubbing feels responsive (15-30fps)
- [ ] No lag or freezing
- [ ] Audio muted during scrub

### Test 8-9: Segment Boundaries
- [ ] Boundary skip is quick (<100ms)
- [ ] Brief flicker acceptable
- [ ] No audio glitches
- [ ] Playhead position correct after skip

### Test 15: Performance
- Activity Monitor readings:
  - Memory: _____ MB
  - CPU: _____ %
  - No memory leaks after 5 minutes playback

## Issues Found

[List any bugs, unexpected behavior, or concerns]

## Pass/Fail

**Overall Result:** [ ] PASS [ ] FAIL

**Notes:**
