# Claude Code Context - Bref

Quick reference for AI-assisted development. See `docs/plans/` for complete specifications.

## Project Overview

**Bref** is a Windows video editor for removing unwanted segments from MP4/H.264 videos.

**Primary Use Case:** Trim Teams meeting recordings, screen captures, and other recordings.

**Target:** Microsoft Store release, 3-month MVP timeline.

## Core Design Decision

**Virtual Timeline (Non-Destructive Editing)**
- Original video NEVER modified
- All edits tracked as timestamps in `SegmentList`
- Playback engine skips deleted segments seamlessly
- Single re-encode only at final export
- Instant deletion preview, clean undo/redo

## Technology Stack

```
UI:       Avalonia UI 11.x (XAML + MVVM)
Language: C# (.NET 8)
Video:    FFmpeg 7.x (hardware acceleration)
Platform: Windows 10/11 (64-bit)
Package:  MSIX (Microsoft Store)
```

## Key Libraries

- `FFmpeg.AutoGen` - Low-level FFmpeg bindings
- `FFMpegCore` - High-level FFmpeg wrapper
- `NAudio` - Waveform generation
- `CommunityToolkit.Mvvm` - MVVM helpers
- `Serilog` - Logging

## MVP Constraints

**INCLUDED:**
- ✅ MP4/H.264 only (validate format strictly)
- ✅ Click-and-drag segment deletion
- ✅ Waveform + thumbnail timeline
- ✅ Auto-optimal export (hardware acceleration)
- ✅ Full undo/redo (50 levels)
- ✅ Save/load projects (.bref files)

**EXCLUDED (defer to v1.1+):**
- ❌ Other formats (AVI, MOV, MKV)
- ❌ Export quality presets
- ❌ Variable speed playback
- ❌ Light theme
- ❌ Localization

## Project Structure

```
src/Bref/
├── Models/              # Data models (VideoProject, SegmentList, etc.)
├── ViewModels/          # MVVM ViewModels
├── Views/               # XAML UI
├── Services/            # Business logic (SegmentManager, PlaybackEngine, etc.)
├── FFmpeg/              # FFmpeg integration layer
└── Utilities/           # Helpers (LRUCache, etc.)
```

## Critical Data Models

**SegmentList** - Virtual timeline (kept segments only)
```csharp
List<VideoSegment> KeptSegments;  // Non-overlapping, sorted by SourceStart
TimeSpan VirtualToSourceTime(TimeSpan virtualTime);
void DeleteSegment(TimeSpan virtualStart, TimeSpan virtualEnd);
```

**VideoSegment** - Continuous portion of source video
```csharp
TimeSpan SourceStart;  // Position in original file
TimeSpan SourceEnd;    // Position in original file
```

**EditHistory** - Undo/redo stack
```csharp
void PushState(SegmentList currentState);
SegmentList Undo(SegmentList currentState);
SegmentList Redo(SegmentList currentState);
```

## Key Services

- **VideoService** - Load/validate videos, extract metadata
- **SegmentManager** - Manage virtual timeline, undo/redo
- **PlaybackEngine** - Play through kept segments seamlessly
- **FrameCache** - LRU cache (60 frames ~370MB for 1080p)
- **WaveformGenerator** - Pre-generate audio waveform on load
- **ThumbnailGenerator** - Pre-generate timeline thumbnails
- **ExportService** - FFmpeg export with hardware acceleration

## Performance Targets

```
Load 1hr video:    < 30 seconds
Timeline scrub:    60 fps
Deletion:          < 100ms (instant)
Export (hardware): 2-5 min for 30min result
Memory usage:      < 500MB typical
```

## Hardware Acceleration

**Priority Order:**
1. `h264_nvenc` (NVIDIA GPU)
2. `h264_qsv` (Intel Quick Sync)
3. `h264_amf` (AMD GPU)
4. `libx264` (Software fallback)

Detect once, cache result, display in status bar.

## Export Strategy

Use FFmpeg `filter_complex` to concatenate segments:

```bash
# Example for 2 segments:
-filter_complex "[0:v]trim=start=0:end=5,setpts=PTS-STARTPTS[v0];
                 [0:a]atrim=start=0:end=5,asetpts=PTS-STARTPTS[a0];
                 [0:v]trim=start=10:end=60,setpts=PTS-STARTPTS[v1];
                 [0:a]atrim=start=10:end=60,asetpts=PTS-STARTPTS[a1];
                 [v0][a0][v1][a1]concat=n=2:v=1:a=1[outv][outa]"
```

## Development Principles

1. **YAGNI** - Resist feature creep, defer complexity
2. **Performance First** - Profile early, optimize critical paths
3. **Test Core Logic** - Unit tests for SegmentList, time conversion
4. **AI-Assisted** - Use Claude Code for boilerplate, focus on creative work

## Common Patterns

**Time Conversion:**
- UI uses "virtual time" (deleted segments removed)
- FFmpeg uses "source time" (original file positions)
- Always convert via `SegmentList.VirtualToSourceTime()`

**State Management:**
- Before any edit: `History.PushState(CurrentSegments.Clone())`
- On undo: Restore previous SegmentList from stack
- Clear redo stack on new action

**Error Handling:**
- Validate MP4/H.264 early (before processing)
- Show user-friendly messages ("Only MP4 H.264 supported")
- Log technical details with Serilog

## Documentation Locations

**Detailed Specs:**
- Architecture: `docs/plans/2025-11-01-architecture.md`
- User Flow: `docs/plans/2025-11-01-user-flow.md`
- Technical: `docs/plans/2025-11-01-technical-specification.md`
- Roadmap: `docs/plans/2025-11-01-mvp-scope-roadmap.md`

**Reference:**
- Full C# code samples in technical specification
- Weekly milestones in roadmap (12 weeks, ~330 hours)

## Current Phase

**Week 0:** Design complete ✅
**Next:** Week 1 - Project setup, FFmpeg POC

---

*Keep this file lightweight - full details in docs/plans/*
