# Week 8 Audio Playback - Manual Testing Checklist

## Test Environment
- [ ] macOS with LibVLC installed
- [ ] Sample video with audio track (sample-30s.mp4)
- [ ] Sample video without audio track

## Task 4: Verify Audio Playback with LibVLC
- [ ] Load video with audio (sample-30s.mp4)
- [ ] Press Space to play
- [ ] Confirm audio plays synchronized with video
- [ ] Check console logs show "Audio playback started: X track(s)"
- [ ] Volume slider shows 100%
- [ ] Adjust volume slider to 50% - audio volume decreases
- [ ] Adjust volume slider to 0% - audio mutes

## Task 7: Mute/Unmute Hotkey
- [ ] Set volume to 80% using slider
- [ ] Press M key - audio mutes, volume shows 0%
- [ ] Press M key again - audio unmutes, volume restores to 80%
- [ ] Repeat mute/unmute cycle multiple times
- [ ] Change volume while muted - stays at new volume when unmuted

## Task 8: No-Audio Video
- [ ] Load video without audio track
- [ ] Press Space to play
- [ ] Video plays normally without errors
- [ ] Volume controls remain functional (no crashes)
- [ ] Console logs show "Audio tracks: 0"

## Task 9: Performance Testing
- [ ] Load large video file (>100MB, >10 minutes)
- [ ] Monitor memory usage in Activity Monitor
- [ ] Play for 5 minutes continuously
- [ ] Verify no memory leaks
- [ ] Verify audio stays synchronized throughout playback
- [ ] Test scrubbing - audio seeks correctly to new position
- [ ] Test segment deletion - audio plays through segment boundary smoothly

## Segment Boundary Audio Behavior
- [ ] Load video, delete middle segment (e.g., 10s-20s)
- [ ] Play from before deleted segment
- [ ] Confirm audio continues seamlessly when jumping over deleted segment
- [ ] No audio glitches or pops at boundary
- [ ] Check console logs show "Audio seek" message at boundary

## Edge Cases
- [ ] Mute, then close and reopen application (volume state not persisted - expected)
- [ ] Set volume to 100%, play loud video, reduce to 10% - responsive
- [ ] Rapid volume changes using slider - no crashes
- [ ] Press M key rapidly multiple times - toggles correctly
