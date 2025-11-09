# Week 7 Playback Engine - Manual Testing Checklist

## Purpose

This document provides a comprehensive manual testing checklist for the Week 7 Playback Engine implementation. These tests verify that video playback works correctly with Play/Pause controls, segment boundary skipping, and frame preloading.

## Prerequisites

Before starting manual tests:
- [ ] Build succeeds: `dotnet build`
- [ ] All unit tests pass: `dotnet test`
- [ ] Application launches without errors
- [ ] Test video file available (MP4/H.264, ideally 5-10 minutes long)

## Test Environment

- **Test Date:** _______________
- **Tester:** _______________
- **Build Commit:** _______________
- **Test Video:** _______________
- **Video Duration:** _______________
- **Video Resolution:** _______________
- **Video Frame Rate:** _______________

---

## Test Scenario 1: Basic Playback Controls

### Objective
Verify that Play/Pause buttons appear and function correctly.

### Steps
1. Launch Sparta Cut application
2. Click "Load MP4 Video" button
3. Select a test video file
4. Verify Play and Pause buttons appear in toolbar
5. Click Play button
6. Observe video playback for 5 seconds
7. Click Pause button
8. Verify video stops at current position

### Expected Results
- [ ] Play button is visible after video loads
- [ ] Pause button is visible after video loads
- [ ] Clicking Play starts smooth video playback
- [ ] Frame updates are visible during playback
- [ ] Timeline cursor moves during playback
- [ ] Clicking Pause stops playback immediately
- [ ] Video remains at paused position (no drift)

### Pass/Fail Criteria
- **PASS:** All frames render smoothly, buttons respond immediately, no lag
- **FAIL:** Frames stutter, buttons unresponsive, video drifts after pause

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Test Scenario 2: Space Key Toggle

### Objective
Verify Space key toggles Play/Pause state correctly.

### Steps
1. Load a video (from Scenario 1 or new session)
2. Press Space key
3. Verify video starts playing
4. Wait 3 seconds
5. Press Space key again
6. Verify video pauses
7. Press Space key once more
8. Verify video resumes

### Expected Results
- [ ] Space key starts playback when stopped
- [ ] Space key pauses playback when playing
- [ ] Space key resumes playback when paused
- [ ] Hotkey responds within 100ms
- [ ] No double-trigger issues (press once = one action)

### Pass/Fail Criteria
- **PASS:** Space key reliably toggles Play/Pause, no missed inputs
- **FAIL:** Space key unresponsive or triggers multiple times per press

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Test Scenario 3: Smooth Frame Updates

### Objective
Verify that frames render smoothly during continuous playback without stuttering.

### Steps
1. Load a video with visible motion (e.g., screen recording with mouse movement)
2. Click Play
3. Observe playback for 30 seconds
4. Watch for frame drops or stuttering
5. Check CPU usage in Task Manager (should be reasonable)

### Expected Results
- [ ] Frames update at consistent rate (30fps or video's native fps)
- [ ] No visible stuttering or frame drops
- [ ] Motion appears smooth and continuous
- [ ] CPU usage stays reasonable (<50% on modern hardware)
- [ ] No memory leaks over 30 seconds

### Pass/Fail Criteria
- **PASS:** Smooth playback with consistent frame rate, no stuttering
- **FAIL:** Visible stuttering, frame drops, or excessive CPU usage (>70%)

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **CPU Usage:** ___________%
- **Notes:** _______________________________________________

---

## Test Scenario 4: Segment Deletion and Seamless Playback

### Objective
Verify that playback seamlessly skips over deleted segments without visual glitches.

### Steps
1. Load a video
2. Select a segment in the middle (e.g., 2:00 to 2:30)
3. Click "Delete Selection"
4. Verify segment is removed from timeline (visual gap)
5. Seek to position before deleted segment (e.g., 1:50)
6. Click Play
7. Observe playback as it approaches deleted segment boundary
8. Verify playback jumps to 2:30 and continues

### Expected Results
- [ ] Timeline shows visual gap where segment was deleted
- [ ] Playback continues smoothly up to deletion boundary
- [ ] Playback skips deleted segment (jumps from 2:00 to 2:30)
- [ ] No pause or freeze at boundary
- [ ] No frame flash or visual artifact during skip
- [ ] Timeline cursor updates correctly after skip

### Pass/Fail Criteria
- **PASS:** Seamless skip over deleted segment, no visible glitch
- **FAIL:** Pause at boundary, frame freeze, or playback stops

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Deleted Segment:** _______ to _______
- **Notes:** _______________________________________________

---

## Test Scenario 5: Play to End Auto-Stop

### Objective
Verify that playback automatically stops when reaching the end of the video.

### Steps
1. Load a short video (or seek near end of long video)
2. Seek to 10 seconds before end
3. Click Play
4. Let video play until end
5. Observe behavior when playback reaches final frame

### Expected Results
- [ ] Playback continues to final frame
- [ ] Playback stops automatically at end
- [ ] Timeline cursor is at end position
- [ ] Play button becomes enabled (can restart)
- [ ] Pause button becomes disabled
- [ ] No crash or hang at end

### Pass/Fail Criteria
- **PASS:** Clean auto-stop at end, UI updates correctly
- **FAIL:** Playback hangs, crashes, or continues past end

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Test Scenario 6: Multiple Segment Deletions

### Objective
Verify that playback correctly handles multiple deleted segments throughout the video.

### Steps
1. Load a video (at least 5 minutes long)
2. Delete 3 non-overlapping segments:
   - Segment A: 1:00 to 1:30
   - Segment B: 2:30 to 3:00
   - Segment C: 4:00 to 4:15
3. Seek to beginning (0:00)
4. Click Play
5. Observe playback through all three deletions
6. Let playback continue to end

### Expected Results
- [ ] Timeline shows 3 visual gaps
- [ ] Playback skips Segment A (jumps 1:00 → 1:30)
- [ ] Playback skips Segment B (jumps 2:30 → 3:00)
- [ ] Playback skips Segment C (jumps 4:00 → 4:15)
- [ ] All skips are seamless (no glitches)
- [ ] Playback reaches end without hanging
- [ ] Timeline cursor position is accurate throughout

### Pass/Fail Criteria
- **PASS:** All three segments skipped cleanly, playback reaches end
- **FAIL:** Any segment not skipped, playback hangs, or visual glitches

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Segment A:** _______ to _______
- **Segment B:** _______ to _______
- **Segment C:** _______ to _______
- **Notes:** _______________________________________________

---

## Test Scenario 7: Seek During Playback

### Objective
Verify that seeking while playing works correctly and playback continues from new position.

### Steps
1. Load a video
2. Click Play
3. While playing, click on timeline at different position (e.g., jump ahead 1 minute)
4. Verify playback continues from new position
5. While playing, click timeline to jump backward
6. Verify playback continues from new position

### Expected Results
- [ ] Clicking timeline during playback seeks immediately
- [ ] Playback continues from new position without stopping
- [ ] Frame updates match new timeline position
- [ ] No freeze or hang during seek
- [ ] Timeline cursor jumps to clicked position

### Pass/Fail Criteria
- **PASS:** Seek works during playback, continues smoothly from new position
- **FAIL:** Playback stops after seek, or position is incorrect

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Test Scenario 8: Frame Rate Accuracy

### Objective
Verify that playback timing matches expected frame rate (frames displayed per second).

### Steps
1. Load a 30fps video
2. Note current time position (e.g., 1:00)
3. Click Play
4. Use stopwatch to measure 10 seconds of real time
5. Click Pause after exactly 10 seconds
6. Check timeline position (should be ~1:10, ±0.2s)

### Expected Results
- [ ] Timeline advances approximately 10 seconds
- [ ] Acceptable drift: ±0.2 seconds over 10 seconds
- [ ] Frame rate appears consistent (no visible speed changes)
- [ ] No acceleration or deceleration during playback

### Pass/Fail Criteria
- **PASS:** Timing accurate within ±0.2s over 10 seconds
- **FAIL:** Drift exceeds ±0.5s, or visible speed inconsistency

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Expected Time:** 10.0s
- **Actual Time:** _______s
- **Drift:** _______s
- **Notes:** _______________________________________________

---

## Test Scenario 9: Undo/Redo During Playback

### Objective
Verify that undo/redo operations work correctly while video is playing.

### Steps
1. Load a video
2. Delete a segment (e.g., 1:00 to 1:30)
3. Start playback from beginning
4. While playing (before reaching deleted segment), press Ctrl+Z (Undo)
5. Verify deleted segment is restored
6. Continue watching playback through previously deleted area
7. Press Ctrl+Y (Redo) while playing
8. Verify segment is deleted again and playback skips it

### Expected Results
- [ ] Undo restores segment during playback
- [ ] Playback continues through restored segment
- [ ] Timeline updates immediately after undo
- [ ] Redo removes segment during playback
- [ ] Playback skips re-deleted segment
- [ ] No crash or hang during undo/redo

### Pass/Fail Criteria
- **PASS:** Undo/redo work during playback, timeline updates correctly
- **FAIL:** Undo/redo cause crash, hang, or incorrect playback behavior

### Results
- **Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Test Scenario 10: Edge Cases

### Objective
Verify edge cases and boundary conditions.

### Test 10a: Play at End Position
1. Load video
2. Seek to end (last frame)
3. Click Play
4. **Expected:** Playback resets to beginning and starts playing

### Test 10b: Play Empty Timeline
1. Load video
2. Delete entire video (select all and delete)
3. Click Play
4. **Expected:** Nothing happens (or error message)

### Test 10c: Rapid Play/Pause
1. Load video
2. Rapidly click Play/Pause 10 times in quick succession
3. **Expected:** No crash, final state is consistent

### Test 10d: Delete Segment During Playback
1. Load video, start playing
2. While playing, delete segment ahead of current position
3. **Expected:** Playback skips deleted segment when reached

### Results
- **10a Status:** ⬜ Pass / ⬜ Fail
- **10b Status:** ⬜ Pass / ⬜ Fail
- **10c Status:** ⬜ Pass / ⬜ Fail
- **10d Status:** ⬜ Pass / ⬜ Fail
- **Notes:** _______________________________________________

---

## Performance Observations

### Frame Preloading
- [ ] Playback is smooth near segment boundaries (preloading working)
- [ ] No noticeable lag before boundary skip
- [ ] Memory usage is reasonable during playback

**Memory Usage During Playback:**
- **Before playback:** _______MB
- **During playback:** _______MB
- **After 5 minutes:** _______MB

### CPU Usage
- **Idle:** ___________%
- **During playback:** ___________%
- **Peak usage:** ___________%

### Responsiveness
- [ ] UI remains responsive during playback
- [ ] Timeline scrubbing works during playback
- [ ] Other buttons (Undo/Redo/Delete) respond immediately

---

## Issues Found

### Critical Issues (Blocking)
_List any issues that prevent core functionality:_

1. _______________________________________________
2. _______________________________________________

### Major Issues (Significant Impact)
_List issues that significantly impact user experience:_

1. _______________________________________________
2. _______________________________________________

### Minor Issues (Cosmetic/Edge Cases)
_List minor issues or cosmetic problems:_

1. _______________________________________________
2. _______________________________________________

---

## Known Limitations (Expected)

These are expected limitations per the Week 7 plan:

- [ ] No audio playback (audio stub only - Week 8 feature)
- [ ] Brief pause may be visible at segment boundaries (smoothing not implemented)
- [ ] Fixed preload count (10 frames) may not be optimal for all frame rates

---

## Overall Test Summary

### Statistics
- **Total Scenarios:** 10
- **Passed:** _______
- **Failed:** _______
- **Blocked:** _______

### Recommendation
⬜ **APPROVED** - Ready for Week 8 (audio implementation)
⬜ **APPROVED WITH ISSUES** - Minor issues acceptable, proceed with caution
⬜ **NOT APPROVED** - Critical issues found, requires fixes before continuing

### Tester Sign-off

**Name:** _______________
**Date:** _______________
**Signature:** _______________

---

## Common Failure Modes Reference

For testers encountering issues, here are common failure patterns:

1. **Stuttering Playback**
   - Symptom: Frames skip or stutter during playback
   - Possible Cause: Frame preloading not working, cache miss
   - Debug: Check logs for "Failed to preload frame" warnings

2. **Boundary Hang**
   - Symptom: Playback freezes at segment boundary
   - Possible Cause: Segment skip logic not detecting deleted segment
   - Debug: Check logs for "Skipping deleted segment" messages

3. **Memory Leak**
   - Symptom: Memory usage increases continuously during playback
   - Possible Cause: Frames not being released from cache
   - Debug: Monitor memory over 10 minutes of playback

4. **Timing Drift**
   - Symptom: Playback time doesn't match real time
   - Possible Cause: Frame timer interval incorrect or timer drift
   - Debug: Check video metadata frame rate vs. timer interval

5. **UI Freeze**
   - Symptom: UI becomes unresponsive during playback
   - Possible Cause: Frame updates on UI thread blocking
   - Debug: Check if frame preloading is running on background thread

---

## Additional Notes

_Space for any additional observations or comments:_

_______________________________________________
_______________________________________________
_______________________________________________
_______________________________________________
