# Claude Code Context - Bref

## Project Overview

**Bref** is a Windows video editor for removing unwanted segments from MP4/H.264 videos.

**Primary Use Case:** Trim Teams meeting recordings, screen captures, and other recordings.

**Target:** Microsoft Store release

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
Platform: Windows 11 (64-bit)
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
- Architecture: `docs/plans/architecture.md`
- User Flow: `docs/plans/user-flow.md`
- Technical: `docs/plans/technical-specification.md`
- Roadmap: `docs/plans/mvp-scope-roadmap.md`

## Critical Rules (Never Violate)
- Keep evything simple so a beginer/medium experimented developer can understand and maintain the code.
- No external library except if it greatly simplify the code and it's a very well maintened library. Always ask me if you want to add a library and expose the pros/cons of building your own code vs adding a library.
- Each time you touch the code, update package version with the following rule: if you just implemented a new important feature, increment the minor version digit; else increment the patch version digit.
- Commit the repository only when I ask. When you create a commit, tag it with the current app verion. Always push after you commited.
- If you learn something interesting and usefull for the rest of the project, update this CLAUDE.md file in section "Today I learned". But before, ask me if your new knowledge is correct.
- If you made a mistake in your interpretation of the specs, architecture, features etc. update this CLAUDE.md file in section "Never again". But before, ask me if your new knowledge is correct.
- Always ask questions when you need clarification or if you have the choice between multiple solutions.
- Always ask for validation before commiting changes
- Never add features that weren't explicitly requested (like the Auto-save toggle I added to Settings). Always implement exactly what was asked for, but DO propose good ideas as suggestions for the user to accept or decline. Frame additional features as questions: "Would you also like me to add [feature], or should we keep it as-is for now?"

## Always Think Step by Step
- Read specification → Check dependencies → Validate data flow → Implement incrementally → Test immediately

## Today I Learned

### Week 7: Playback Engine Architecture (January 2025)

**Timer-Based Playback Design:**
- System.Timers.Timer provides frame-accurate playback at configurable frame rates (30/60fps)
- Frame interval calculation: `1000.0 / frameRate` milliseconds
- Timer events drive the playback loop, advancing CurrentTime by one frame duration each tick
- Non-blocking frame preloading (Task.Run) prevents UI freezes during playback

**Segment Boundary Handling:**
- Use `SegmentList.SourceToVirtualTime()` to detect deleted segments (returns null if in deleted region)
- When playback hits a deleted segment, automatically jump to next kept segment's SourceStart
- This creates seamless playback that "skips over" deleted content
- No special UI handling needed - the timeline naturally reflects the virtual time

**PlaybackEngine Integration Pattern:**
- PlaybackEngine owns Timer, FrameCache, and SegmentManager references
- Exposes events: TimeChanged (every frame) and StateChanged (play/pause/stop)
- MainWindowViewModel subscribes to events and updates Timeline.CurrentTime
- This event-driven architecture keeps UI responsive and decoupled from playback logic

**Frame Preloading Strategy:**
- Preload 10 frames ahead (~333ms buffer at 30fps)
- Preloading runs async in background (non-blocking)
- FrameCache's LRU eviction naturally manages memory
- Preloading is essential for smooth playback across segment boundaries

**Audio Synchronization (Deferred to Week 8):**
- AudioPlayer stub created with volume control and seek methods
- NAudio requires extracting audio to temp file (can't read MP4 directly)
- Future implementation: FFmpeg audio extraction + NAudio playback synchronized with video timer

### Week 8+: LibVLC Integration (November 2025)

**LibVLCSharp Architecture:**
- LibVLCSharp.Avalonia provides native VideoView control for Avalonia
- Core.Initialize() must be called once before creating LibVLC instances
- MediaPlayer.Time is in milliseconds, convert to/from TimeSpan
- Seek operations are async - set _isSeeking flag to prevent monitoring during seek

**Position Monitoring for Segment Boundaries:**
- Timer-based polling (50-100ms) is optimal for boundary detection
- Check SegmentManager.SourceToVirtualTime() - returns null if in deleted segment
- Immediate seek to next segment creates brief flicker (acceptable UX)
- Seek throttling (50ms) prevents overwhelming VLC with rapid seeks

**Hybrid FFmpeg + LibVLC Approach:**
- FFmpeg excels at static analysis: thumbnails, waveform, metadata extraction
- LibVLC excels at dynamic playback: smooth rendering, audio sync, hardware acceleration
- Keep both: FFmpeg for analysis, LibVLC for playback
- This split simplifies codebase while leveraging each tool's strengths

**Scrubbing with VLC:**
- VLC seek is fast enough for 15-30fps scrubbing experience
- Throttle seeks to 50-100ms during timeline drag
- No need for FrameCache - VLC handles buffering internally
- User experience is responsive without complex frame caching logic

**Memory and Performance:**
- VLC internal buffers: ~50-100MB (vs FrameCache: ~370MB)
- Net savings: ~200MB less memory usage
- Position monitor (50ms timer): <5% CPU overhead
- Boundary detection precision: 2-3 frames (66-100ms at 30fps)

**Cleanup Benefits:**
- Removed 1500+ lines of complex FFmpeg frame management code
- Simplified architecture: VLC for playback, FFmpeg for analysis only
- VideoFrame model retained (still needed for PersistentFrameDecoder in thumbnails/waveform)
- Clear separation of concerns improves maintainability

## Never Again

*No mistakes documented yet - add lessons learned from errors here.*
