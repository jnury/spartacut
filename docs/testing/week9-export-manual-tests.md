# Week 9 Export Service - Manual Testing Checklist

**Status:** ✅ COMPLETED (Week 9 - January 2025)
**Implementation:** FFmpeg export with hardware acceleration

## Prerequisites
- Video file with audio (MP4/H.264) - use `samples/sample-30s.mp4`
- At least 1GB free disk space for exports
- FFMpegCore installed ✅

---

## Test 1: Single Segment Export (No Deletions)

**Steps:**
1. Load `samples/sample-30s.mp4`
2. No deletions - keep entire video
3. Click Export button
4. Save as `test-single-segment.mp4`
5. Wait for export to complete

**Expected:**
- [ ] Progress bar updates from 0-100%
- [ ] Export completes successfully
- [ ] Output file size similar to source (~30MB)
- [ ] Output plays correctly with audio
- [ ] Duration matches source (30 seconds)

---

## Test 2: Multiple Segment Export (With Deletions)

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete segment: 10-15 seconds
3. Delete segment: 20-25 seconds
4. Click Export button
5. Save as `test-multi-segment.mp4`

**Expected:**
- [ ] Progress bar updates smoothly
- [ ] Export completes successfully
- [ ] Output duration: 20 seconds (30 - 10 deleted)
- [ ] Deleted segments missing in output
- [ ] Audio remains synchronized

---

## Test 3: Hardware Acceleration Detection

**Steps:**
1. Check console logs on app startup
2. Look for encoder detection messages

**Expected:**
- [ ] Logs show hardware encoders detected (or "No hardware encoders")
- [ ] NVENC detected on NVIDIA systems
- [ ] Quick Sync detected on Intel systems
- [ ] AMF detected on AMD systems
- [ ] Software fallback (libx264) if no hardware

---

## Test 4: Export Progress Accuracy

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete segment: 5-10 seconds
3. Start export
4. Monitor progress bar and time estimates

**Expected:**
- [ ] Progress updates every second
- [ ] Percentage increases steadily 0-100%
- [ ] Elapsed time shown
- [ ] Estimated time remaining shown
- [ ] Time estimates reasonably accurate

---

## Test 5: Export Cancellation

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Start export
3. Click Cancel when progress reaches 30-50%

**Expected:**
- [ ] Export cancels immediately
- [ ] Partial output file may exist
- [ ] UI returns to ready state
- [ ] Can start new export after cancellation

---

## Test 6: Output File Overwrite

**Steps:**
1. Export to `test-overwrite.mp4`
2. Export again to same filename
3. Confirm overwrite

**Expected:**
- [ ] Warning shown (or automatic overwrite)
- [ ] Second export succeeds
- [ ] File replaced with new export

---

## Test 7: Invalid Source File

**Steps:**
1. Delete source video file
2. Try to export

**Expected:**
- [ ] Error message: "Source video not found"
- [ ] Export does not start
- [ ] No crash

---

## Test 8: No Segments to Export

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete entire video (0-30 seconds)
3. Try to export

**Expected:**
- [ ] Error message: "No segments to export"
- [ ] Export button disabled
- [ ] No crash

---

## Test 9: Very Long Video (Performance)

**Steps:**
1. Load a 1-hour+ video (if available)
2. Delete multiple segments
3. Export

**Expected:**
- [ ] Export starts successfully
- [ ] Progress updates regularly
- [ ] Completes in reasonable time (5x realtime with hardware)
- [ ] Memory usage stays <1GB

---

## Test 10: Software Encoding Fallback

**Steps:**
1. Force software encoding (PreferredEncoder = "libx264")
2. Export video with deletions

**Expected:**
- [ ] Export works with software encoding
- [ ] Slower than hardware (~1x realtime)
- [ ] Output quality good

---

## Results Summary

**Date Tested:** __________
**Tester:** __________

| Test | Status | Notes |
|------|--------|-------|
| 1. Single segment | ☐ Pass | |
| 2. Multiple segments | ☐ Pass | |
| 3. Hardware detection | ☐ Pass | |
| 4. Progress accuracy | ☐ Pass | |
| 5. Cancellation | ☐ Pass | |
| 6. Overwrite | ☐ Pass | |
| 7. Invalid source | ☐ Pass | |
| 8. No segments | ☐ Pass | |
| 9. Long video | ☐ Pass | |
| 10. Software fallback | ☐ Pass | |

**Overall Status:** ☐ All Pass ☐ Issues Found

**Issues:**
