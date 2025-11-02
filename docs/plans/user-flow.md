# Bref - User Flow Documentation

**Date:** 2025-11-01
**Version:** 1.0 - MVP Design
**Status:** Approved

## Overview

This document details the complete user experience for Bref, a video editing tool designed for quickly removing unwanted segments from MP4/H.264 videos using an iterative, preview-based workflow.

**Core User Experience Principles:**
- Instant feedback on all operations
- Clear visual communication of state
- Undo/redo for error recovery
- Minimal clicks to accomplish tasks
- Keyboard shortcuts for efficiency

## Complete User Journey

### 1. Application Launch

**Entry Point:** User double-clicks Bref icon or opens from Start Menu

**Initial State:**
```
┌─────────────────────────────────────────┐
│ Bref                              ═ □ ✕ │
├─────────────────────────────────────────┤
│                                         │
│              [Bref Logo]                │
│                                         │
│         ┌─────────────────┐             │
│         │  Open Video     │             │
│         └─────────────────┘             │
│                                         │
│         Drop video file here            │
│         Supported: MP4 (H.264)          │
│                                         │
│  Recent Files:                          │
│  • meeting_2025-10-28.mp4               │
│  • presentation_recording.mp4           │
│                                         │
└─────────────────────────────────────────┘
```

**User Actions:**
1. Click "Open Video" button → Opens file picker
2. Drag-drop video file onto window → Instant import
3. Click recent file → Loads previous video
4. File → Open Video (Ctrl+O) → Opens file picker

**Validation:**
- If file is not MP4: "Error: Only MP4 (H.264) videos are supported"
- If file is corrupted: "Error: Cannot read video file. File may be corrupted"
- If codec is not H.264: "Error: Video must use H.264 codec"

---

### 2. Video Import & Loading

**Trigger:** User selects valid MP4/H.264 video file

**Loading Screen:**
```
┌─────────────────────────────────────────┐
│ Bref - Loading...                 ═ □ ✕ │
├─────────────────────────────────────────┤
│                                         │
│         Loading: meeting.mp4            │
│                                         │
│  ✓ Validating format                    │
│  ✓ Reading metadata                     │
│  ⏳ Generating waveform... 45%          │
│  ⏳ Generating thumbnails... 30%        │
│                                         │
│  ████████████░░░░░░░░░░░ 60%            │
│                                         │
│  Cancel                                 │
│                                         │
└─────────────────────────────────────────┘
```

**Time Estimates:**
- Validation: 1-2 seconds
- Metadata: 1-2 seconds
- Waveform generation: 5-10 seconds (1-hour video)
- Thumbnail generation: 5-15 seconds (1-hour video)
- **Total: 15-30 seconds for 1-hour video**

**User Experience:**
- Progress bar shows overall completion
- Detailed steps show current operation
- Cancel button available (returns to start screen)
- Animated spinner indicates activity

---

### 3. Main Editing Interface

**View After Load:**
```
┌─────────────────────────────────────────────────────────┐
│ Bref - meeting.mp4                            ═ □ ✕     │
├─────────────────────────────────────────────────────────┤
│ File   Edit                                             │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │                                                 │    │
│  │            [Video Preview Frame]                │    │
│  │                1920x1080                        │    │
│  │                                                 │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  ▶ ⏮ ⏭    00:00:00 / 00:45:30    🔊 ▂▃▅▆▇              │
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │ [Thumbnail][Thumbnail][Thumbnail][Thumbnail]... │    │
│  │ ▁▂▃▅▇█▇▅▃▂▁▁▂▃▅▇ Waveform ▇▅▃▂▁▁▂▃▅▇█▇▅▃▂▁      │    │
│  │ │                                               │    │
│  │ 00:00      00:15      00:30      00:45          │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  ↶ Undo  ↷ Redo    🗑 Delete Segment    ⬇ Export        │
│                                                         │
│ Ready │ Duration: 00:45:30 │ Segments: 1 │ NVENC        │
└─────────────────────────────────────────────────────────┘
```

**Initial State:**
- Playhead at 00:00:00
- Single segment (full video)
- Undo/Redo disabled
- Delete Segment disabled (no selection)
- Export enabled

---

### 4. Navigation & Review

**Goal:** User explores video to identify unwanted segments

#### 4a. Timeline Scrubbing

**Action:** User clicks and drags on timeline

**Behavior:**
- Playhead follows mouse cursor
- Video preview updates in real-time (60fps target)
- Current time display updates
- Smooth frame interpolation via cache

**Visual Feedback:**
- Red vertical playhead line follows cursor
- Time tooltip shows exact timestamp on hover
- Waveform helps identify audio activity
- Thumbnails help identify visual content

#### 4b. Play/Pause

**Action:** User clicks Play (▶) or presses Space

**Behavior:**
- Video plays from current playhead position
- Playhead advances smoothly
- Audio plays synchronized
- Button changes to Pause (⏸)

**Controls:**
- Space: Toggle play/pause
- Click preview: Pause
- Scrub timeline: Auto-pause

#### 4c. Frame-by-Frame Navigation

**Action:** User presses Left/Right arrow keys or clicks ⏮/⏭ buttons

**Behavior:**
- Left Arrow / ⏮: Previous frame (-33ms for 30fps video)
- Right Arrow / ⏭: Next frame (+33ms for 30fps video)
- Hold key: Continuous stepping (10 fps rate)
- Video preview updates immediately

**Use Case:** Finding exact cut points with precision

---

### 5. Segment Selection

**Goal:** User marks unwanted segment for deletion

#### Step-by-Step Interaction

**Step 1: Navigate to segment start**
- User scrubs or plays to beginning of unwanted content
- Example: 5:00 mark (boring intro starts)

**Step 2: Click-and-drag selection**
- User clicks on timeline at 5:00
- User drags mouse to 10:00 (end of boring intro)

**Visual Feedback During Drag:**
```
Timeline:
┌─────────────────────────────────────────────────┐
│ [Thumbnail][Thumbnail][■■■Selected■■■][Thumb]   │
│ ▁▂▃▅▇█▇▅▃▂▁[▓▓▓Orange▓▓▓]▂▁▁▂▃▅▇█▇▅▃▂▁          │
│ │              ├─────────┤                      │
│ 00:00        05:00    10:00        00:45        │
└─────────────────────────────────────────────────┘
```

- Selected region highlighted in orange (40% opacity)
- Selection handles at start/end (draggable to adjust)
- Time tooltip shows: "Selected: 05:00 - 10:00 (5:00 duration)"

**Step 3: Adjust selection (optional)**
- Drag handles to fine-tune start/end points
- Use arrow keys for frame-precise adjustment
- Selection updates in real-time

**Result:**
- Delete Segment button becomes enabled (orange highlight)
- Status bar shows: "Selection: 05:00 - 10:00 (5:00)"

---

### 6. Segment Deletion

**Trigger:** User presses DELETE key or clicks "Delete Segment" button

#### Immediate Visual Changes

**Before Deletion:**
```
Segments: [00:00:00 ─────────────── 00:45:30]
Duration: 00:45:30
```

**After Deletion (INSTANT):**
```
Segments: [00:00:00 ─── 00:05:00] [00:10:00 ─── 00:45:30]
Duration: 00:40:30  (5 minutes removed)
```

**Timeline Updates:**
- Selected segment disappears
- Timeline contracts (no gap shown)
- Thumbnails/waveform reflow
- Playhead repositions to just before deletion point (05:00)
- Duration updates in status bar: "Duration: 00:40:30"
- Segment count updates: "Segments: 2"

**UI State Changes:**
- Undo button becomes enabled
- Selection cleared
- Delete Segment button disabled again
- Status: "Ready - 5:00 removed"

**Response Time:** <100ms (instant to user)

---

### 7. Preview After Deletion

**Goal:** Verify deletion looks correct

#### Option A: Immediate Playback Test

**Action:** User clicks Play (Space)

**Behavior:**
- Plays from 04:55 (just before deletion)
- Reaches 05:00 (end of first segment)
- **Seamlessly** jumps to 10:00 (start of second segment)
- No visible gap or hiccup
- Audio continuous

**User Perception:** Video plays as if deleted content never existed

#### Option B: Scrub Across Deletion

**Action:** User drags playhead across deletion boundary

**Behavior:**
- Scrubbing from 04:00 → 05:30 smoothly
- At 05:00, automatically shows frame from 10:00 (second segment)
- Virtual timeline is continuous (no gap)
- Tooltip shows both times: "05:00 (source: 10:00)"

---

### 8. Iterative Editing

#### Scenario A: Satisfied with Deletion → Delete Another

**Flow:**
1. User scrubs to find next unwanted segment (e.g., 20:00-25:00 in virtual time)
2. Click-drag to select
3. Press DELETE
4. Timeline updates again (now 3 segments, 35:30 duration)
5. Preview
6. Repeat as needed

**State After Multiple Deletions:**
```
Original: [00:00:00 ──────────────────────── 00:45:30]

After 3 deletions:
Segment 1: [00:00:00 ─── 00:05:00]  (5 min)
Segment 2: [10:00 ──── 20:00]      (10 min)
Segment 3: [25:00 ──── 35:00]      (10 min)
Segment 4: [40:00 ──── 45:30]      (5.5 min)

Virtual Duration: 00:30:30
Segments: 4
```

#### Scenario B: Made a Mistake → Undo

**Trigger:** User realizes last deletion was wrong

**Action:** Press Ctrl+Z or click Undo button

**Behavior:**
- Timeline reverts to previous state
- Deleted segment reappears
- Duration restores
- Playhead position preserved
- Status: "Undo: Deletion restored"
- Redo button becomes enabled

**Multiple Undo:**
- Each Ctrl+Z steps back one deletion
- Can undo all the way to original video
- Redo (Ctrl+Y) steps forward through history

---

### 9. Export Final Video

**Trigger:** User clicks "Export" button (Ctrl+E)

#### Step 1: Choose Output Location

**Dialog:**
```
┌─────────────────────────────────────────┐
│ Export Video                          × │
├─────────────────────────────────────────┤
│                                         │
│ Save As: meeting_edited.mp4             │
│ Location: C:\Users\...\Videos           │
│                                         │
│ Settings:                               │
│ Quality: Auto-optimal                   │
│ Encoder: NVENC (Hardware)               │
│                                         │
│ Original: 00:45:30 → 00:30:30           │
│ Estimated size: ~450 MB                 │
│ Estimated time: 3-5 minutes             │
│                                         │
│          [Cancel]  [Export]             │
└─────────────────────────────────────────┘
```

**User Actions:**
- Change filename if desired
- Click "Export" to begin

#### Step 2: Export Progress

**Progress Screen:**
```
┌─────────────────────────────────────────┐
│ Exporting...                        ═ □ │
├─────────────────────────────────────────┤
│                                         │
│ Exporting: meeting_edited.mp4           │
│                                         │
│ ████████████░░░░░░░░░░░ 60%             │
│                                         │
│ Phase: Encoding video (NVENC)           │
│ Processed: 18:18 / 30:30                │
│ Time remaining: ~2 minutes              │
│                                         │
│ Speed: 10.2x realtime                   │
│ Output size: 270 MB                     │
│                                         │
│              [Cancel]                   │
│                                         │
└─────────────────────────────────────────┘
```

**Details:**
- Progress bar shows overall completion (0-100%)
- Time remaining updated every 0.5 seconds
- Real-time speed indicator (10x = fast, 1x = slow)
- Cancel button available (aborts export, deletes partial file)

**Time Estimates:**
- 30-minute result with NVENC: 2-5 minutes
- 30-minute result with software: 10-20 minutes

#### Step 3: Export Complete

**Success Screen:**
```
┌─────────────────────────────────────────┐
│ Export Complete                     ✓ × │
├─────────────────────────────────────────┤
│                                         │
│       [✓ Success Icon]                  │
│                                         │
│ Video exported successfully!            │
│                                         │
│ File: meeting_edited.mp4                │
│ Size: 445 MB                            │
│ Duration: 00:30:30                      │
│ Time taken: 3:24                        │
│                                         │
│  [Open Folder]  [Export Another]  [OK]  │
│                                         │
└─────────────────────────────────────────┘
```

**User Actions:**
- "Open Folder" → Opens file explorer to output location
- "Export Another" → Returns to edit view (same video)
- "OK" → Closes dialog, stays in application

---

### 10. Session Management

#### Save Project

**Purpose:** Save edit history without exporting video

**Action:** File → Save Project (Ctrl+S)

**Dialog:**
```
Save Bref Project

Save As: meeting_edited.bref
Location: C:\Users\...\Videos

[Cancel]  [Save]
```

**Project File Contents:**
- Source video path
- Segment list (timestamps only)
- Last playhead position
- Window size/position

**File Size:** <10 KB (no video data)

#### Load Project

**Action:** File → Load Project

**Behavior:**
- Opens file picker (.bref files)
- Loads source video
- Restores segment list and edit history
- Positions playhead to saved location
- Skips thumbnail/waveform regeneration (cached)

**Use Case:** Resume editing session on different machine or day

#### Close with Unsaved Changes

**Trigger:** User closes window with deletions but no export

**Dialog:**
```
┌─────────────────────────────────────────┐
│ Unsaved Changes                     ! × │
├─────────────────────────────────────────┤
│                                         │
│ You have unsaved changes.               │
│                                         │
│ Do you want to save your project?       │
│                                         │
│  [Save Project]  [Discard]  [Cancel]    │
│                                         │
└─────────────────────────────────────────┘
```

**User Actions:**
- "Save Project" → Save dialog → Exit
- "Discard" → Exit without saving
- "Cancel" → Return to editing

---

## Error Scenarios

### During Import

**Error: Invalid Format**
```
┌─────────────────────────────────────────┐
│ Error                               ✕ × │
├─────────────────────────────────────────┤
│                                         │
│ Cannot open this video file.            │
│                                         │
│ Reason: Not an MP4 H.264 video          │
│                                         │
│ Bref currently only supports:           │
│ • MP4 container format                  │
│ • H.264 (AVC) video codec               │
│                                         │
│ Try converting your video with          │
│ HandBrake or VLC first.                 │
│                                         │
│              [OK]                       │
└─────────────────────────────────────────┘
```

### During Export

**Error: Out of Disk Space**
```
┌─────────────────────────────────────────┐
│ Export Failed                       ✕ × │
├─────────────────────────────────────────┤
│                                         │
│ Export failed: Insufficient disk space  │
│                                         │
│ Required: ~450 MB                       │
│ Available: 128 MB                       │
│                                         │
│ Free up disk space and try again.       │
│                                         │
│  [Save Project]  [Retry]  [Cancel]      │
└─────────────────────────────────────────┘
```

---

## Keyboard Shortcuts Reference

**Playback:**
- `Space` - Play/Pause
- `Left Arrow` - Previous frame
- `Right Arrow` - Next frame
- `Home` - Jump to start
- `End` - Jump to end

**Editing:**
- `Delete` - Delete selected segment
- `Ctrl+Z` - Undo
- `Ctrl+Y` - Redo
- `Escape` - Clear selection

**File Operations:**
- `Ctrl+O` - Open video
- `Ctrl+S` - Save project
- `Ctrl+E` - Export video
- `Ctrl+Q` - Quit application

**Timeline:**
- `Mouse Wheel` - Zoom timeline
- `Click-Drag` - Select segment
- `Shift+Click` - Extend selection

---

## Accessibility Considerations

**Keyboard Navigation:**
- All features accessible via keyboard
- Tab navigation through controls
- Focus indicators visible

**Visual Indicators:**
- High contrast selection colors
- Clear button states (enabled/disabled)
- Progress indicators for long operations

**Error Messages:**
- Clear, actionable language
- No technical jargon
- Suggested solutions provided

---

## Performance Expectations

**Responsiveness:**
- UI actions: <50ms
- Timeline scrubbing: 60fps
- Deletion: <100ms (instant)
- Undo/redo: <50ms (instant)

**Loading:**
- Small videos (<10 min): 5-10 seconds
- Medium videos (30-60 min): 15-30 seconds
- Large videos (2+ hours): 45-90 seconds

**Export:**
- Hardware acceleration: 2-10x realtime
- Software fallback: 0.5-2x realtime

---

## Conclusion

This user flow provides a streamlined, intuitive experience for video editing focused on segment removal. The iterative workflow with instant preview and undo/redo support enables users to confidently refine their videos without fear of mistakes.

Key success factors:
- ✓ Instant feedback on all operations
- ✓ Clear visual communication
- ✓ Minimal learning curve
- ✓ Keyboard shortcuts for efficiency
- ✓ Graceful error handling

**Ready for implementation.**
