# Bref - MVP Scope & Roadmap

**Date:** 2025-11-01
**Version:** 1.0 - MVP Definition
**Status:** Approved

## Executive Summary

This document defines the exact scope for Bref's Minimum Viable Product (MVP) - a focused, releasable video editing tool for removing unwanted segments from MP4/H.264 videos. The MVP targets a **3-month development timeline** with clear milestones and deliverables.

**MVP Goal:** Ship a functional, performant video segment removal tool to Microsoft Store that solves the core use case: trimming Teams meeting recordings and similar screen capture videos.

---

## MVP Scope

### ✅ Included in MVP

#### Core Features

**1. Video Import**
- Open MP4 files with H.264 codec only
- Drag-and-drop support
- Recent files list (last 10)
- Format validation with clear error messages
- Metadata extraction (duration, resolution, framerate, etc.)

**2. Timeline Visualization**
- Audio waveform display
- Video thumbnail strip (5-second intervals)
- Time ruler with markers
- Smooth 60fps scrubbing
- Zoom in/out timeline (mouse wheel)

**3. Video Preview**
- Real-time preview window
- Aspect ratio maintained
- Frame-accurate display
- Synchronized with timeline playhead

**4. Playback Controls**
- Play/Pause (Space bar)
- Frame-by-frame stepping (Left/Right arrows)
- Timeline cursor seeking (click anywhere)
- Audio volume control
- Current time / duration display

**5. Segment Deletion (Iterative Workflow)**
- Click-and-drag timeline selection
- Selection adjustment with handles
- Delete segment (Delete key or button)
- Instant visual feedback (<100ms)
- Virtual timeline (non-destructive editing)
- Seamless playback across segment boundaries

**6. Undo/Redo**
- Full undo/redo support (Ctrl+Z / Ctrl+Y)
- 50 levels of history
- Visual button states (enabled/disabled)

**7. Export**
- Auto-optimal quality settings
- Hardware acceleration detection (NVENC/Quick Sync/AMF)
- Software fallback (libx264)
- Progress bar with time estimates
- Cancel during export
- Success/error notifications

**8. Session Management**
- Save project (.bref files with timestamps only)
- Load project (restore edit session)
- Unsaved changes warning on close

**9. UI/UX**
- Single window application
- Dark theme (modern, clean)
- Keyboard shortcuts for all main actions
- Status bar with segment count, duration, encoder
- Responsive layout (minimum 1280x720)

#### Technical Requirements

**Performance Targets:**
- Load 1-hour video: <30 seconds
- Timeline scrubbing: 60fps
- Deletion operation: <100ms
- Export with hardware: 2-5 minutes for 30-min result
- Memory usage: <500MB typical

**Platform:**
- Windows 10 (version 1809+) and Windows 11
- 64-bit only
- Minimum 8GB RAM, 16GB recommended

**Distribution:**
- Microsoft Store (MSIX package)
- ~100MB download size
- Automatic updates via Store

---

### ❌ Explicitly Excluded from MVP

**Defer to Post-MVP (v1.1+):**

1. **Multiple Video Formats**
   - AVI, MOV, MKV, WebM support
   - Codec auto-conversion
   - *Reason:* MP4/H.264 covers 90% of use cases; adds complexity

2. **Variable Speed Playback**
   - 0.5x, 2x, 4x playback speeds
   - *Reason:* Nice-to-have; not critical for core workflow

3. **Jump to Timestamp**
   - Type "1:23:45" to jump directly
   - *Reason:* Timeline scrubbing sufficient for MVP

4. **Export Quality Presets**
   - Fast/Balanced/Quality options
   - Custom bitrate/resolution controls
   - *Reason:* Auto-optimal covers most cases; adds UI complexity

5. **Light Theme**
   - Theme switcher
   - *Reason:* Dark theme sufficient; reduces testing scope

6. **Localization**
   - French, Spanish, etc. translations
   - *Reason:* English-first MVP; i18n adds complexity

7. **Advanced Timeline Features**
   - Multiple zoom presets
   - Minimap overview
   - Markers/bookmarks
   - *Reason:* Basic timeline sufficient for core workflow

8. **Batch Processing**
   - Process multiple videos in queue
   - *Reason:* Single video focus for MVP

9. **Video Filters**
   - Brightness/contrast adjustment
   - Color correction
   - *Reason:* Out of scope for segment removal tool

10. **Multi-Track Editing**
    - Separate video/audio timeline
    - Audio ducking
    - *Reason:* Complex feature; defer to v2.0

---

## Development Roadmap

### Month 1: Foundation (Weeks 1-4)

#### Week 1: Project Setup & POC
**Goal:** Development environment ready, basic FFmpeg integration working

**Tasks:**
- Create Avalonia project structure
- Install dependencies (Avalonia, FFmpeg.AutoGen, FFMpegCore, NAudio)
- Bundle FFmpeg binaries
- Implement HardwareAccelerationDetector
- POC: Load MP4, extract single frame, display in Avalonia
- Setup logging (Serilog)
- Initialize git repository

**Deliverable:** Can open MP4 file and display first frame

**Estimated Effort:** 20 hours

---

#### Week 2: Video Loading & Metadata
**Goal:** Complete video import pipeline with validation

**Tasks:**
- Implement VideoService (format validation, metadata extraction)
- Implement VideoMetadata model
- Create loading screen UI
- Implement WaveformGenerator (NAudio integration)
- Add progress reporting for waveform generation
- Add error handling for invalid files
- Unit tests for VideoService

**Deliverable:** Can load MP4 with progress bar, extract metadata, generate waveform

**Estimated Effort:** 25 hours

---

#### Week 3: Timeline UI & Thumbnails
**Goal:** Custom timeline control displaying waveform and thumbnails

**Tasks:**
- Implement ThumbnailGenerator
- Create TimelineControl (custom Avalonia control)
- Render waveform visualization
- Render thumbnail strip
- Draw time ruler with markers
- Implement playhead rendering
- Add click-to-seek functionality

**Deliverable:** Timeline displays waveform + thumbnails, click to move playhead

**Estimated Effort:** 30 hours

---

#### Week 4: Frame Cache & Preview
**Goal:** Smooth video preview with playback controls

**Tasks:**
- Implement LRUCache utility
- Implement FrameCache (60-frame cache)
- Implement FrameDecoder (hardware-accelerated)
- Create VideoPlayerControl
- Implement scrubbing (real-time preview during timeline drag)
- Add frame-by-frame stepping
- Performance optimization (target 60fps scrubbing)

**Deliverable:** Smooth timeline scrubbing with real-time preview

**Estimated Effort:** 30 hours

---

### Month 2: Core Editing (Weeks 5-8)

#### Week 5: Segment Manager & Data Models
**Goal:** Virtual timeline logic fully implemented

**Tasks:**
- Implement VideoSegment model
- Implement SegmentList with VirtualToSourceTime conversion
- Implement SegmentManager
- Implement EditHistory (undo/redo stack)
- Unit tests for SegmentList (time conversion, deletions)
- Unit tests for EditHistory
- Integration test: Multiple deletions + undo/redo

**Deliverable:** Segment management logic complete and tested

**Estimated Effort:** 25 hours

---

#### Week 6: Selection & Deletion UI
**Goal:** User can select and delete segments

**Tasks:**
- Implement TimelineSelection model
- Add click-and-drag selection to TimelineControl
- Render selection highlight with handles
- Implement selection adjustment (drag handles)
- Wire up Delete button/hotkey
- Update timeline after deletion (contract segments)
- Update status bar (duration, segment count)

**Deliverable:** Can select and delete segments with instant UI update

**Estimated Effort:** 25 hours

---

#### Week 7: Playback Engine
**Goal:** Seamless playback across segment boundaries

**Tasks:**
- Implement PlaybackEngine
- Play/Pause functionality
- Continuous playback through multiple segments
- Frame preloading near segment boundaries
- Audio synchronization
- Playback state management
- Handle edge cases (end of video, empty segments)

**Deliverable:** Smooth playback through deleted segments

**Estimated Effort:** 30 hours

---

#### Week 8: Undo/Redo Integration
**Goal:** Full undo/redo with UI integration

**Tasks:**
- Wire Undo command to SegmentManager
- Wire Redo command to SegmentManager
- Update UI after undo/redo (timeline, preview, buttons)
- Enable/disable Undo/Redo buttons based on state
- Add Ctrl+Z / Ctrl+Y hotkeys
- Manual testing of complex undo/redo scenarios

**Deliverable:** Complete undo/redo workflow

**Estimated Effort:** 20 hours

---

### Month 3: Export & Polish (Weeks 9-12)

#### Week 9: Export Service Implementation
**Goal:** Export video with hardware acceleration

**Tasks:**
- Implement ExportService
- Build FFmpeg filter_complex for segment concatenation
- Execute FFmpeg with progress monitoring
- Parse FFmpeg output for progress percentage
- Implement cancel functionality
- Test with NVENC, Quick Sync, software fallback
- Export dialog UI

**Deliverable:** Can export edited video with progress bar

**Estimated Effort:** 30 hours

---

#### Week 10: Session Management
**Goal:** Save/load projects, handle unsaved changes

**Tasks:**
- Design .bref file format (JSON)
- Implement project serialization/deserialization
- Save Project dialog and logic
- Load Project dialog and logic
- Recent files list
- Unsaved changes warning on close
- File associations (.mp4, .bref)

**Deliverable:** Can save/load editing sessions

**Estimated Effort:** 20 hours

---

#### Week 11: UI Polish & Testing
**Goal:** Professional UI, keyboard shortcuts, edge cases

**Tasks:**
- Implement all keyboard shortcuts
- Add tooltips to all controls
- Status bar implementation
- Menu bar (File, Edit menus)
- Error dialogs (format errors, export failures)
- Success notifications
- Handle edge cases (very short videos, single-frame deletions)
- Performance profiling and optimization
- Memory leak testing

**Deliverable:** Polished, bug-free UI

**Estimated Effort:** 25 hours

---

#### Week 12: Packaging & Release
**Goal:** Microsoft Store ready package

**Tasks:**
- Create MSIX package manifest
- Design application icon and Store assets
- Test MSIX installation/uninstallation
- Write Store listing (description, screenshots)
- Create user documentation (README)
- Final testing on clean Windows 10 and 11 machines
- Submit to Microsoft Store for review

**Deliverable:** Application published on Microsoft Store

**Estimated Effort:** 25 hours

---

## Total Effort Estimate

**Total Hours:** ~330 hours over 12 weeks

**Breakdown:**
- Month 1 (Foundation): 105 hours
- Month 2 (Core Editing): 100 hours
- Month 3 (Export & Polish): 100 hours
- Buffer for unexpected issues: 25 hours

**Assumes:**
- Full-time development (35-40 hours/week)
- AI assistance (Claude Code) for 20-30% productivity boost
- Minimal context switching

---

## Success Criteria

### MVP Launch Criteria

**Functional Requirements:**
- ✅ Can import MP4/H.264 videos reliably
- ✅ Timeline displays waveform and thumbnails
- ✅ Can delete segments with instant preview
- ✅ Playback is smooth across segment boundaries
- ✅ Undo/redo works correctly
- ✅ Export produces valid video files
- ✅ Hardware acceleration works on NVIDIA/Intel/AMD
- ✅ No crashes during typical workflow

**Performance Requirements:**
- ✅ Load 1-hour video in <30 seconds
- ✅ Timeline scrubbing at 50+ fps
- ✅ Deletion operation <100ms
- ✅ Memory usage <600MB peak
- ✅ Export at 5x+ realtime (hardware)

**Quality Requirements:**
- ✅ Zero critical bugs
- ✅ No data loss (corrupted exports)
- ✅ Clear error messages for all failure modes
- ✅ Passes Microsoft Store certification

### Post-Launch KPIs (First 3 Months)

**Adoption:**
- 1,000+ downloads from Microsoft Store
- 100+ active weekly users

**Quality:**
- <5% crash rate
- <10% negative reviews
- Average rating >4.0 stars

**Performance:**
- Average export time <5 minutes (30-min video)
- Average load time <20 seconds (1-hour video)

---

## Risk Management

### High-Risk Items

**1. Hardware Acceleration Reliability**
- **Risk:** NVENC/Quick Sync may not work on some systems
- **Mitigation:** Robust detection, automatic fallback to software, clear status indicator
- **Contingency:** Document known issues, provide troubleshooting guide

**2. FFmpeg Complexity**
- **Risk:** filter_complex syntax errors cause export failures
- **Mitigation:** Extensive testing with various segment configurations, unit tests
- **Contingency:** Simplified export mode (sequential re-encode if filter fails)

**3. Timeline Performance**
- **Risk:** Scrubbing may lag with large videos
- **Mitigation:** Frame cache, thumbnail resolution tuning, profiling
- **Contingency:** Reduce thumbnail density, proxy video option

**4. Segment Boundary Playback**
- **Risk:** Visible/audible gaps between segments during playback
- **Mitigation:** Frame preloading, careful audio buffer management
- **Contingency:** Accept minor hiccups, document as known limitation

### Medium-Risk Items

**5. Memory Usage**
- **Risk:** May exceed 1GB with very long videos
- **Mitigation:** Cache size tuning, thumbnail compression, streaming waveform
- **Contingency:** Document minimum 16GB RAM for 2+ hour videos

**6. Microsoft Store Review**
- **Risk:** Rejection due to runFullTrust capability or other policy
- **Mitigation:** Study policies carefully, justify all permissions
- **Contingency:** Distribute via GitHub releases until approved

---

## Post-MVP Roadmap

### Version 1.1 (Month 4)
**Focus:** Quick wins and user feedback

**Features:**
- Multiple format support (AVI, MOV, MKV)
- Variable speed playback (0.5x, 2x)
- Export quality presets (Fast/Balanced/Quality)
- Light theme option
- Performance improvements based on telemetry

### Version 1.2 (Month 5-6)
**Focus:** Advanced features

**Features:**
- Jump to timestamp
- Timeline bookmarks/markers
- Batch processing (multiple videos)
- Improved thumbnail generation (scene detection)

### Version 2.0 (Month 7-12)
**Focus:** Professional features

**Features:**
- Multi-track timeline (separate video/audio)
- Basic transitions between segments
- Audio normalization/ducking
- Keyboard shortcut customization
- Localization (French, Spanish, German)
- Cross-platform (macOS, Linux via Avalonia)

### Version 3.0 (Year 2+)
**Focus:** Ecosystem expansion

**Features:**
- Cloud project sync
- Collaboration features (shared projects)
- Plugin system for custom effects
- API for automation
- Command-line interface

---

## Development Principles

### YAGNI (You Aren't Gonna Need It)
- Resist feature creep
- Defer complexity until proven necessary
- Ship minimal, working software

### Iterative Development
- Weekly builds with working features
- Continuous testing on real videos
- Early performance profiling

### AI-Assisted Development
- Use Claude Code for boilerplate (models, services)
- AI code review for security and performance
- Focus human effort on creative problem-solving

### Quality Gates
- All features must have unit tests
- No commit breaks the build
- Performance targets validated weekly
- Manual testing before each milestone

---

## Resource Requirements

### Development Environment
- Windows 10/11 PC (primary dev machine)
- Visual Studio 2022 Community Edition (free)
- Visual Studio Code (for documentation)
- Git + GitHub (version control)

### Hardware for Testing
- NVIDIA GPU machine (NVENC testing)
- Intel CPU with Quick Sync (QSV testing)
- AMD GPU machine (AMF testing)
- Low-spec machine (8GB RAM, integrated GPU) for minimum spec testing

### Tools & Services
- Microsoft Store Developer Account ($19 one-time)
- Claude Code subscription (if needed)
- GitHub account (free tier sufficient)

### Total Cost: ~$50 (excluding hardware)

---

## Conclusion

This MVP scope is deliberately focused on the core value proposition: **removing unwanted segments from videos quickly and reliably**. By limiting format support, export options, and advanced features, we can deliver a polished, bug-free product in 3 months.

The phased roadmap provides clear direction for post-MVP growth while maintaining the "simple, fast, focused" philosophy.

**Key Success Factors:**
1. Strict scope discipline (resist feature creep)
2. Weekly milestones with working software
3. Early and continuous performance testing
4. AI-assisted development for productivity
5. Focus on user experience over technical perfection

**Ready to build.** 🚀
