# Claude Code Context - Bref

## Project Overview

**Bref** is a Windows video editor for removing unwanted segments from MP4/H.264 videos.
**Target:** Microsoft Store release (MSIX)

## Core Design: Virtual Timeline (Non-Destructive)
- Original video NEVER modified
- Edits tracked as timestamps in `SegmentList`
- Playback skips deleted segments seamlessly
- Single re-encode at export

## Technology Stack

```
UI:       Avalonia UI 11.x (XAML + MVVM)
Language: C# (.NET 8)
Playback: LibVLCSharp 3.8.5 (video rendering)
Analysis: FFMpegCore 5.1.0 (metadata/thumbnails/waveform/export)
Platform: macOS (dev), Windows 11 (target)
Package:  MSIX (Microsoft Store)
```

## Key Libraries

- `LibVLCSharp` / `LibVLCSharp.Avalonia` - Video playback (via system VLC)
- `FFMpegCore` - Video analysis & export (shells out to ffmpeg binary)
- `NAudio` - Audio waveform generation
- `CommunityToolkit.Mvvm` - MVVM helpers
- `Serilog` - Logging

## Critical Data Models

**SegmentList** - Virtual timeline (kept segments only)
```csharp
List<VideoSegment> KeptSegments;  // Non-overlapping, sorted by SourceStart
TimeSpan? SourceToVirtualTime(TimeSpan sourceTime);  // Returns null if in deleted segment
```

**EditHistory** - Undo/redo (50 levels)

## Key Services

- **VlcPlaybackEngine** - LibVLC-based playback with segment boundary jumping
- **VideoService** - Load/validate videos via FFMpegCore
- **SegmentManager** - Manage virtual timeline, undo/redo
- **ThumbnailGenerator** - FFMpegCore snapshot extraction
- **WaveformGenerator** - Audio extraction + peak generation
- **ExportService** - FFMpegCore export with hardware acceleration

## Common Patterns

**Time Conversion:**
- UI uses "virtual time" (deleted segments removed)
- Source uses "source time" (original file positions)
- Convert via `SegmentList.SourceToVirtualTime()`

**Segment Boundary Handling:**
- Position monitor (50ms timer) checks `SourceToVirtualTime()`
- Returns null → in deleted segment → seek to next segment's SourceStart + 100ms
- Track `_lastSeekTarget` to prevent infinite loops on slow CPUs

## Critical Rules
- Keep everything simple for beginner/intermediate developers
- No external libraries without approval (justify pros/cons vs building)
- Version bumps: minor for new features, patch otherwise
- Commit only when asked, tag with version, always push
- Ask before updating "Today I Learned" or "Never Again" sections
- Always ask for validation before committing changes
- Never add unrequested features - propose as questions instead

## Development Setup (macOS)

**LibVLC requires x64 (Rosetta 2) on Apple Silicon:**
```bash
# Build x64 self-contained
dotnet publish src/Bref/Bref.csproj --runtime osx-x64 --self-contained true -c Debug

# Run via Rosetta 2
arch -x86_64 ./src/Bref/bin/Debug/net8.0/osx-x64/publish/Bref
```

**Why:** LibVLCSharp doesn't support ARM64 natively on macOS. FFMpegCore works cross-arch (shells out to binary).

## Today I Learned

### Week 9: LibVLC Migration (January 2025)

**Why LibVLC:**
- Replaced complex FFmpeg frame decoding (~1500 lines) with VLC's MediaPlayer
- Native hardware acceleration, smooth playback, automatic audio sync
- Memory: 50-100MB (VLC internal) vs 370MB (old FrameCache)

**Architecture Split:**
- **LibVLC:** Dynamic playback/scrubbing (VideoView control)
- **FFMpegCore:** Static analysis (metadata, thumbnails, waveform, export)

**Segment Boundary Jumping:**
- Timer polls position every 50ms
- Detect deleted segment via `SourceToVirtualTime() == null`
- Seek to `nextSegment.SourceStart + 100ms` buffer
- Track last seek target to prevent loops regardless of CPU speed

**macOS x64 Requirement:**
- LibVLCSharp ARM64 unsupported → run via Rosetta 2
- FFMpegCore architecture-independent (shells out to native ffmpeg)
- MSBuild copies VLC libs/plugins from `/Applications/VLC.app`

**Seek Loop Prevention:**
```csharp
private TimeSpan _lastSeekTarget;
if (Math.Abs((targetTime - _lastSeekTarget).TotalMilliseconds) > 50)
    Seek(targetTime);  // Only seek if different target
```

## Never Again

*No mistakes documented yet*
