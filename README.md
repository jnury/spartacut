# Bref

**A fast, focused video editor for removing unwanted segments from videos.**

Bref is a Windows desktop application designed for quickly trimming videos by removing unwanted segments. Perfect for cleaning up Teams meeting recordings, screen captures, and other MP4 videos.

## Features

- **Iterative Editing:** Delete segments with instant preview and seamless playback
- **Full Undo/Redo:** 50 levels of edit history
- **Visual Timeline:** Waveform and thumbnail display for easy navigation
- **Hardware Acceleration:** Fast exports using NVENC, Quick Sync, or AMF
- **Non-Destructive:** Original video never modified until export
- **Simple UX:** Single window, keyboard shortcuts, minimal learning curve

## Technology Stack

**UI Framework:** Avalonia UI 11.x
- Cross-platform .NET UI framework
- GPU-accelerated rendering
- XAML-based MVVM architecture

**Runtime:** .NET 8.0
- C# 12 language features
- Windows 11+ target platform

**Video Processing:** FFmpeg 7.x
- Hardware-accelerated encoding/decoding
- Industry-standard video manipulation

**Key Libraries:**
- `FFmpeg.AutoGen` - Low-level FFmpeg bindings
- `FFMpegCore` - High-level FFmpeg wrapper
- `NAudio` - Audio waveform generation
- `CommunityToolkit.Mvvm` - MVVM helpers

## Repository Structure

```
Bref/
├── docs/
│   ├── stack-analysis.md              # Technology evaluation research
│   └── plans/                     # Design documentation
│       ├── architecture.md
│       ├── user-flow.md
│       ├── technical-specification.md
│       └── mvp-scope-roadmap.md
├── src/
│   ├── Bref/                      # Main application (to be created)
│   └── Bref.Tests/                # Unit tests (to be created)
├── assets/                         # Icons, FFmpeg binaries (to be created)

├── claude.md                       # Claude Code context reference
└── README.md                       # This file
```

## Development Status

**Current Phase:** Design Complete ✅
**Next Phase:** Week 1 - Project Setup & POC

See `docs/plans/2025-11-01-mvp-scope-roadmap.md` for detailed timeline.

## MVP Scope (v1.0)

**Included:**
- MP4/H.264 video support
- Click-and-drag segment deletion
- Timeline with waveform and thumbnails
- Instant preview and undo/redo
- Auto-optimal export with hardware acceleration
- Save/load project sessions

**Deferred to Post-MVP:**
- Multiple format support (AVI, MOV, MKV)
- Export quality presets
- Variable speed playback
- Localization

## Quick Start (Coming Soon)

```bash
# Install dependencies
dotnet restore

# Run application
dotnet run --project src/Bref

# Run tests
dotnet test
```

## Documentation

- **[Architecture](docs/plans/2025-11-01-architecture.md)** - System design and data flow
- **[User Flow](docs/plans/2025-11-01-user-flow.md)** - Complete user experience walkthrough
- **[Technical Spec](docs/plans/2025-11-01-technical-specification.md)** - Implementation details with code samples
- **[MVP Roadmap](docs/plans/2025-11-01-mvp-scope-roadmap.md)** - 3-month development plan

## Design Principles

1. **YAGNI** - Ship minimal, focused software
2. **Performance First** - Hardware acceleration, 60fps UI
3. **Non-Destructive** - Original video never modified
4. **Iterative Workflow** - Instant feedback on all operations

## Target Platform

- **OS:** Windows 11
- **Architecture:** 64-bit only
- **RAM:** 8GB minimum, 16GB recommended
- **GPU:** NVIDIA/Intel/AMD for hardware encoding (optional)

## Distribution

**Microsoft Store** (MSIX package)
- Automatic updates
- Clean install/uninstall
- ~100MB download size

## License

TBD

## Contributing

TBD

---

**Built with:** Avalonia UI + C# + FFmpeg
**Developed with:** Claude Code assistance
