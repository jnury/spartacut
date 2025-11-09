# Bref - Video Editor Context

**Target:** Windows video editor for MP4/H.264 segment removal → Microsoft Store (MSIX)

## Stack
- **UI:** Avalonia 11.x (XAML + MVVM), C# .NET 8
- **Playback:** LibVLCSharp 3.8.5 (requires x64 Rosetta on macOS M-series)
- **Analysis:** FFMpegCore 5.1.0, NAudio, Serilog
- **MVVM:** CommunityToolkit.Mvvm

## Core Architecture
- **Virtual Timeline:** Edits tracked as timestamps in `SegmentList`, original never modified
- **SegmentManager:** Undo/redo (50 levels), virtual↔source time conversion
- **VlcPlaybackEngine:** Segment boundary jumping (50ms timer, seeks past deleted regions)

## CRITICAL: Button State Pattern

**DO THIS** - Observable property + IsEnabled binding:
```csharp
[ObservableProperty]
private bool _undoEnabled = false;

UndoEnabled = _segmentManager.CanUndo;  // Direct update

[RelayCommand]  // NO CanExecute!
public async Task Undo() {
    if (!_segmentManager.CanUndo) return;
    ...
}
```
```xml
<Button Command="{Binding UndoCommand}" IsEnabled="{Binding UndoEnabled}"/>
```

**DON'T DO:**
- ❌ `[RelayCommand(CanExecute = nameof(CanUndo))]` - unreliable in Avalonia
- ❌ Mix Command + manual IsEnabled - they conflict
- ❌ `OnPropertyChanged(nameof(CanUndo))` on computed properties - doesn't work

## macOS Development

**Build/Run (x64 required for LibVLC):**
```bash
cd src && ./build-and-run.sh  # Clean build script
```

**Manual:**
```bash
dotnet publish src/Bref/Bref.csproj --runtime osx-x64 --self-contained -c Debug
arch -x86_64 ./src/Bref/bin/Debug/net8.0/osx-x64/publish/Bref
```

## Key Patterns
- **Time:** UI=virtual (post-deletions), Source=original file positions
- **Segment Jumps:** Poll `SourceToVirtualTime()` → null = deleted → seek to next+100ms
- **Audio:** LibVLC handles playback/sync automatically, no manual coordination

## Rules
- Version bumps: patch for fixes, minor for features
- Commit/tag/push only when asked
- Ask before updating this file
- No unrequested features

## Never Again
**Button State Hell:** RelayCommand CanExecute doesn't reliably update button states in Avalonia. Use simple observable properties (see pattern above). Hours wasted trying CanExecute variants - all failed.
