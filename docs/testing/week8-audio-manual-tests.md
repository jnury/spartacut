# Week 8 Audio Playback - Manual Testing Checklist

## Prerequisites
- Video file with audio track (MP4/H.264)
- Speakers or headphones connected
- Week 8 implementation complete

---

## Test 1: Basic Audio Playback

**Objective:** Verify audio plays synchronized with video

**Steps:**
1. Launch Bref
2. Load MP4 video with audio
3. Click Play button (or press Space)
4. Listen to audio while watching video

**Expected Results:**
- [ ] Audio starts when video starts
- [ ] Audio is synchronized with video (lip sync matches)
- [ ] Audio plays continuously without stuttering
- [ ] Audio quality is clear (no distortion)

**Pass/Fail:** _____ Notes: _____

---

## Test 2: Volume Control

**Objective:** Verify volume slider works correctly

**Steps:**
1. Load video with audio
2. Start playback
3. Move volume slider from 100% to 0%
4. Move back to 50%
5. Move to 100%

**Expected Results:**
- [ ] Volume decreases smoothly from 100% to 0%
- [ ] At 0%, no audio is heard
- [ ] Volume increases smoothly back to 50%
- [ ] At 100%, audio is at full volume
- [ ] Percentage label updates correctly

**Pass/Fail:** _____ Notes: _____

---

## Test 3: Pause/Resume Audio

**Objective:** Verify audio pauses and resumes with video

**Steps:**
1. Load video and start playback
2. Click Pause (or press Space)
3. Wait 2 seconds
4. Click Play (or press Space)

**Expected Results:**
- [ ] Audio stops when video pauses
- [ ] No audio glitches during pause
- [ ] Audio resumes from correct position
- [ ] Audio/video stay synchronized after resume

**Pass/Fail:** _____ Notes: _____

---

## Test 4: Timeline Seeking with Audio

**Objective:** Verify audio seeks correctly with timeline

**Steps:**
1. Load video and start playback
2. Click on timeline at different positions
3. Verify audio jumps to correct position

**Expected Results:**
- [ ] Audio seeks to clicked timeline position
- [ ] Audio stays synchronized with video frame
- [ ] No audio pops or clicks during seek
- [ ] Seeking works both forward and backward

**Pass/Fail:** _____ Notes: _____

---

## Test 5: Segment Deletion with Audio

**Objective:** Verify audio skips deleted segments

**Steps:**
1. Load video with audio
2. Select and delete a middle segment (e.g., 10-15 seconds)
3. Play from before deletion point
4. Observe what happens at deletion boundary

**Expected Results:**
- [ ] Audio plays normally before deleted segment
- [ ] Audio jumps seamlessly to next segment
- [ ] No audio glitch at segment boundary
- [ ] Audio stays synchronized with video

**Pass/Fail:** _____ Notes: _____

---

## Test 6: Multiple Segment Deletions

**Objective:** Verify audio handles complex editing

**Steps:**
1. Load video
2. Delete 3 different segments
3. Play through entire video

**Expected Results:**
- [ ] Audio skips all 3 deleted segments
- [ ] Transitions are smooth at all boundaries
- [ ] Audio/video stay synchronized throughout
- [ ] No cumulative sync drift

**Pass/Fail:** _____ Notes: _____

---

## Test 7: Stop and Replay

**Objective:** Verify Stop button resets audio position

**Steps:**
1. Load video and play to middle
2. Click Stop button
3. Click Play again

**Expected Results:**
- [ ] Audio stops when Stop is clicked
- [ ] Audio position resets to beginning
- [ ] Clicking Play starts from beginning
- [ ] Audio/video synchronized from start

**Pass/Fail:** _____ Notes: _____

---

## Test 8: Video Without Audio Track

**Objective:** Verify graceful handling of no-audio videos

**Steps:**
1. Load MP4 video without audio track
2. Attempt to play

**Expected Results:**
- [ ] Video loads successfully
- [ ] No error messages displayed
- [ ] Video plays normally (silent)
- [ ] Volume slider is disabled or grayed out
- [ ] Log shows "continuing without audio" message

**Pass/Fail:** _____ Notes: _____

---

## Test 9: Temp File Cleanup

**Objective:** Verify temp audio files are cleaned up

**Steps:**
1. Note location of temp folder (check logs for "Extracted audio to..." path)
2. Load video (creates temp WAV file)
3. Close Bref application
4. Check temp folder

**Expected Results:**
- [ ] Temp WAV file is created during load
- [ ] Temp WAV file is deleted on app close
- [ ] No orphaned audio files remain

**Pass/Fail:** _____ Notes: _____

---

## Test 10: Rapid Play/Pause

**Objective:** Verify audio handles rapid state changes

**Steps:**
1. Load video
2. Rapidly press Space key to toggle Play/Pause 10 times
3. Then play normally for 5 seconds

**Expected Results:**
- [ ] No audio glitches or crashes
- [ ] Audio starts/stops cleanly each time
- [ ] Final playback is synchronized
- [ ] No memory leaks (check Task Manager)

**Pass/Fail:** _____ Notes: _____

---

## Performance Observations

**Memory Usage:**
- Before loading video: _____ MB
- After loading video: _____ MB
- During playback: _____ MB
- After 5 minutes playback: _____ MB

**CPU Usage:**
- Idle: _____ %
- During playback: _____ %
- During seeking: _____ %

**Audio Extraction Time:**
- 30-second video: _____ seconds
- 5-minute video: _____ seconds
- 30-minute video: _____ seconds

---

## Issues Found

### Critical
- Issue 1: _____
- Issue 2: _____

### Major
- Issue 1: _____
- Issue 2: _____

### Minor
- Issue 1: _____
- Issue 2: _____

---

## Overall Assessment

**All tests passed:** YES / NO

**Ready for release:** YES / NO

**Notes:** _____

**Tested by:** _____ **Date:** _____
