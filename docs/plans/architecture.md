# Sparta Cut - Architecture Design

**Date:** 2025-11-01
**Version:** 1.0 - MVP Design
**Status:** Approved

## Executive Summary

Sparta Cut is a Windows video editing application for cutting unwanted segments from MP4/H.264 videos using an intuitive iterative workflow. Built with Avalonia UI + C# + FFmpeg, it uses a virtual timeline approach for non-destructive editing with instant preview and clean undo/redo.

**Key Design Decision:** Virtual Timeline (Approach 1)
- Original video never modified
- All edits tracked as segment timestamps
- Single re-encode only at final export
- Instant deletion preview and undo/redo

## Three-Layer Architecture

### 1. UI Layer (Avalonia XAML + MVVM)

**Components:**
- `MainWindow.axaml` - Single window application
- `MainViewModel` - Orchestrates all operations, manages edit history
- `TimelineControl` - Custom control for waveform, thumbnails, segment selection
- `VideoPlayerControl` - Displays current frame, handles playback

**Responsibilities:**
- User input handling (mouse, keyboard)
- Visual rendering (video preview, timeline, waveform)
- Data binding to ViewModels
- Command routing (Play, Delete, Undo, Export)

### 2. Service Layer (Business Logic)

**Core Services:**

**VideoService**
- Loads video metadata
- Validates format (MP4/H.264 only)
- Provides video information to UI

**SegmentManager** (Heart of the system)
- Maintains list of "kept segments" (non-deleted portions)
- Handles undo/redo stack
- Converts between virtual and source timeline positions
- Processes deletion operations

**PlaybackEngine**
- Custom player that seeks through kept segments only
- Skips deleted parts automatically
- Manages playback state (play/pause/seeking)
- Coordinates with FrameCache for smooth playback

**ExportService**
- Re-encodes final video from kept segments
- Uses hardware acceleration (NVENC/Quick Sync/AMF)
- Reports progress to UI
- Handles export errors

**FrameCache**
- LRU cache for decoded frames (60 frames ≈ 370MB for 1080p)
- Preloads frames around playhead for smooth scrubbing
- Hardware-accelerated frame decoding

**WaveformGenerator**
- Pre-generates audio waveform data on video load
- Provides amplitude data for timeline visualization
- Processes in chunks to manage memory

**ThumbnailGenerator**
- Pre-generates timeline thumbnails on video load
- Creates small preview images at regular intervals (every 5 seconds)
- Optimizes timeline visual navigation

### 3. FFmpeg Integration Layer

**Components:**

**FFmpegWrapper**
- Low-level FFmpeg operations using FFmpeg.AutoGen
- Direct P/Invoke to FFmpeg libraries
- Manages FFmpeg contexts and memory

**HardwareAccelerationDetector**
- Detects available encoders (NVENC/Quick Sync/AMF)
- Provides fallback to software encoding
- Auto-selects optimal encoder

**FrameDecoder**
- Hardware-accelerated frame extraction
- Seeks to specific timestamps
- Decodes frames for preview and cache

**VideoEncoder**
- Hardware-accelerated re-encoding for export
- Builds FFmpeg filter_complex for segment concatenation
- Handles audio stream copying

## Key Design Principles

1. **Non-Destructive Editing**
   - Original video file is NEVER modified
   - All edits are reversible until export
   - Edit history saved in lightweight project files

2. **Virtual Timeline**
   - User sees continuous timeline with deleted parts removed
   - Playback engine creates illusion of seamless video
   - Constant translation between virtual ↔ source timestamps

3. **Performance First**
   - Hardware acceleration for all video operations
   - LRU frame cache for responsive scrubbing
   - Pre-generated waveform and thumbnails
   - Single re-encode only at final export

4. **Simplicity**
   - Single window, focused UI
   - Minimal configuration (auto-optimal export)
   - Keyboard shortcuts for power users
   - Clear visual feedback

5. **Iterative Workflow**
   - Instant deletion preview
   - Full undo/redo support
   - Continuous refinement until satisfied
   - Low-friction editing process

## Data Flow

### Video Load Sequence

```
User selects video file
    ↓
VideoService validates format (H.264 check)
    ↓
Extract metadata (duration, resolution, framerate)
    ↓
[Parallel] WaveformGenerator processes audio
[Parallel] ThumbnailGenerator extracts preview frames
    ↓
SegmentManager initializes with single segment (full video)
    ↓
UI displays timeline with waveform and thumbnails
```

### Deletion Sequence

```
User selects segment on timeline (drag)
    ↓
TimelineSelection updated (virtualStart, virtualEnd)
    ↓
User presses DELETE key
    ↓
MainViewModel → SegmentManager.DeleteSegment()
    ↓
SegmentManager pushes current state to undo stack
    ↓
SegmentManager splits/removes segments
    ↓
Timeline UI updates (deleted segment disappears)
    ↓
Playhead repositions if necessary
```

### Playback Sequence

```
User clicks Play button
    ↓
PlaybackEngine starts timer (60fps)
    ↓
For each frame:
    - Get current virtual time
    - SegmentManager converts to source time
    - FrameCache retrieves/decodes frame
    - VideoPlayerControl displays frame
    - Advance virtual time by frame interval
    ↓
Check segment boundary
    - If end of segment reached, jump to next segment
    - Seamless transition (preloaded frames)
    ↓
Continue until user pauses or reaches end
```

### Export Sequence

```
User clicks Export button
    ↓
ExportService receives SegmentList
    ↓
HardwareAccelerationDetector finds best encoder
    ↓
Build FFmpeg filter_complex for concatenation
    ↓
FFmpeg processes video:
    - Read segments from source file
    - Encode with hardware acceleration
    - Copy audio stream (no re-encode)
    - Report progress to UI
    ↓
Output file written
    ↓
Success notification to user
```

## Technology Stack

**Framework:** Avalonia UI 11.x
- Cross-platform capable (Windows primary)
- GPU-accelerated rendering (Skia/Direct2D)
- XAML-based UI definition
- MVVM architecture support

**Language:** C# (.NET 8)
- Modern language features
- Excellent tooling (Visual Studio 2022)
- Strong typing and compile-time safety
- Good Claude Code compatibility (7-8/10)

**Video Processing:** FFmpeg 7.x
- Industry-standard video processing
- Hardware acceleration support (NVENC/QSV/AMF)
- Comprehensive codec support
- Proven reliability

**Libraries:**
- `FFmpeg.AutoGen` - Low-level FFmpeg bindings
- `FFMpegCore` - High-level FFmpeg wrapper for export
- `NAudio` - Audio processing for waveform
- `CommunityToolkit.Mvvm` - MVVM helpers

## Performance Targets

**Loading:**
- Video metadata extraction: <2 seconds
- Waveform generation: 5-10 seconds for 1-hour video
- Thumbnail generation: 5-15 seconds for 1-hour video
- Total load time: <30 seconds for 1-hour video

**Playback:**
- Timeline scrubbing: 60fps
- Seek latency: <100ms
- Frame-by-frame stepping: <50ms response
- Smooth segment transitions: no visible gaps

**Editing:**
- Deletion operation: <100ms (instant to user)
- Undo/redo: <50ms (instant to user)
- Memory usage: <500MB with video loaded

**Export:**
- 30-minute result with NVENC: 2-5 minutes
- 30-minute result with software: 10-20 minutes
- Progress updates: Every 0.5 seconds

## Scalability Considerations

**Video Length:**
- Tested up to: 2-hour videos
- Practical limit: 4-hour videos
- Constraint: Thumbnail/waveform generation time

**Video Resolution:**
- Primary target: 1080p (1920x1080)
- Supported: 720p, 1440p, 4K
- Consideration: Frame cache size adjusts based on resolution

**Number of Deletions:**
- No hard limit on number of segments
- Each deletion is O(n) where n = current segment count
- Practical limit: 100+ deletions without performance impact

**Memory Management:**
- Frame cache: Fixed at 60 frames (~370MB for 1080p)
- Waveform data: ~500KB for 1-hour video
- Thumbnails: ~2-5MB for 1-hour video (720 thumbnails)
- Total: <1GB for typical 1-hour 1080p video

## Error Handling Strategy

**Validation Errors:**
- Invalid format detected early (before processing)
- Clear error messages with suggested actions
- No partial loading states

**Runtime Errors:**
- FFmpeg errors captured and logged
- Graceful degradation where possible
- User-friendly error messages

**Export Errors:**
- Disk space checked before export
- Atomic file writes (temp file → rename)
- Offer to save project on export failure

**Memory Errors:**
- Frame cache eviction prevents OOM
- Thumbnail generation in batches
- Waveform streaming processing

## Testing Strategy

**Unit Tests:**
- SegmentManager logic (add/remove/convert times)
- Virtual ↔ Source time conversion
- Undo/redo stack operations
- Frame cache LRU eviction

**Integration Tests:**
- Video loading end-to-end
- Deletion + playback workflow
- Export with various segment configurations
- Hardware acceleration detection

**UI Tests:**
- Timeline selection interaction
- Keyboard shortcuts
- Drag-and-drop video import

**Performance Tests:**
- Load time for various video lengths
- Scrubbing responsiveness
- Memory usage under load
- Export speed benchmarks

## Security Considerations

**File Access:**
- Read-only access to source videos
- Write access only to explicit export locations
- No network access required

**User Data:**
- Project files contain only timestamps and file paths
- No video data stored in project files
- No telemetry or analytics

**Dependencies:**
- FFmpeg from official sources only
- NuGet packages verified
- Regular security updates

## Deployment

**Platform:** Windows 11 (64-bit)

**Distribution:** Microsoft Store (MSIX package)
- Single-project MSIX packaging
- Automatic updates via Store
- Clean install/uninstall

**Bundle Size:**
- Application + .NET runtime: ~15-30MB
- FFmpeg libraries: ~50-80MB
- Total: ~100MB download

**Requirements:**
- OS: Windows 11 24H2 or later
- RAM: 8GB minimum, 16GB recommended
- CPU: Modern multi-core (hardware acceleration preferred)
- GPU: NVIDIA/Intel/AMD for hardware encoding (optional)
- Disk: 2GB free space for application + temp files

## Future Expansion

**Post-MVP Features:**
- Multiple video format support (AVI, MOV, MKV, WebM)
- Export quality presets (Fast/Balanced/Quality)
- Variable speed playback (0.5x, 2x)
- Jump to timestamp input
- Multiple undo/redo UI history visualization
- Cross-platform (macOS, Linux via Avalonia)
- Dark/Light theme toggle
- Localization (French, etc.)

**Advanced Features (v2.0+):**
- Multi-track timeline (video + audio separate)
- Basic transitions between segments
- Audio ducking/normalization
- Batch processing multiple videos
- Cloud project sync

## Conclusion

This architecture provides a solid foundation for SpartaCut's MVP, balancing performance, user experience, and development speed. The virtual timeline approach enables instant feedback and clean undo/redo, while hardware acceleration ensures smooth playback and fast exports.

The three-layer architecture maintains clear separation of concerns, making the codebase maintainable and testable. FFmpeg integration provides professional-grade video processing capabilities, while Avalonia UI delivers a modern, responsive interface.

**Ready for implementation.**
