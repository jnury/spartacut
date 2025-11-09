# LibVLC Migration - Week 9 (January 2025)

## Summary

Replaced custom FFmpeg frame decoding playback engine with LibVLCSharp for simpler, more robust video playback.

**Result:** Removed ~1500 lines of complex code, reduced memory by 200MB, improved playback smoothness.

## Architecture Changes

### Before (FFmpeg-based)
```
VideoService → FrameExtractor (FFmpeg.AutoGen) → PersistentFrameDecoder
                                                  ↓
Timeline ← PlaybackEngine (Timer + FrameCache) ← RGB24 frames
```

### After (LibVLC-based)
```
VideoService → FrameExtractor (FFMpegCore)
Timeline ← VlcPlaybackEngine (LibVLC MediaPlayer) ← Native video rendering
```

## Key Changes

| Component | Old | New |
|-----------|-----|-----|
| Playback | Custom FFmpeg frame decoder | LibVLCSharp MediaPlayer |
| Metadata | FFmpeg.AutoGen | FFMpegCore (ffprobe) |
| Thumbnails | FFmpeg.AutoGen frame decode | FFMpegCore snapshot |
| Waveform | FFmpeg.AutoGen audio | FFMpegCore audio extraction |
| Memory | ~370MB (FrameCache) | ~50-100MB (VLC internal) |
| Code | ~1900 lines | ~400 lines |

## Deleted Files

- `PersistentFrameDecoder.cs` (~300 lines)
- `FrameCache.cs` (~400 lines)
- `LRUCache.cs` (~150 lines)
- `PlaybackEngine.cs` (~300 lines)
- `VideoPlayerControl.axaml/.cs` (~200 lines)
- `VideoFrame.cs` (~110 lines)
- `FFmpegSetup.cs` (~100 lines)
- All unit tests for above (~850 lines)

## macOS Development Setup

**Problem:** LibVLCSharp doesn't support ARM64 natively on macOS (M1/M2/M4).

**Solution:** Run via x64/Rosetta 2 emulation.

### Build
```bash
dotnet publish src/SpartaCut/SpartaCut.csproj \
  --runtime osx-x64 \
  --self-contained true \
  -c Debug
```

### Run
```bash
arch -x86_64 ./src/SpartaCut/bin/Debug/net8.0/osx-x64/publish/SpartaCut
```

### MSBuild Integration
- Copies VLC libs from `/Applications/VLC.app/Contents/MacOS/lib` to `bin/*/lib/`
- Copies VLC plugins from `/Applications/VLC.app/Contents/MacOS/plugins` to `bin/*/plugins/`
- Sets `VLC_PLUGIN_PATH` environment variable in static constructor

## Segment Boundary Handling

**Challenge:** Skip deleted segments seamlessly during playback.

**Solution:** Position monitoring + smart seeking

```csharp
// Every 50ms during playback
var virtualTime = _segmentManager.SourceToVirtualTime(currentTime);
if (virtualTime == null) // In deleted segment
{
    var nextSegment = KeptSegments.FirstOrDefault(s => s.SourceStart > currentTime);
    var targetTime = nextSegment.SourceStart + 100ms;  // Buffer

    // Prevent infinite loop on slow CPUs
    if (Math.Abs((targetTime - _lastSeekTarget).TotalMilliseconds) > 50)
        Seek(targetTime);
}
```

**Key mechanisms:**
1. **100ms buffer** - Seek past segment boundary to ensure we're inside kept region
2. **150ms seek delay** - `_isSeeking` flag prevents position checks during seek
3. **Last seek tracking** - Don't re-seek to same position (CPU-independent safety)

## Windows Deployment

**No changes needed** - LibVLCSharp supports Windows ARM64 and x64 natively.

The `RuntimeIdentifier` condition only applies to macOS:
```xml
<RuntimeIdentifier Condition="$([MSBuild]::IsOSPlatform('OSX'))">osx-x64</RuntimeIdentifier>
```

## Benefits

✅ **Simpler:** 1500 fewer lines of complex frame management
✅ **Faster:** Hardware-accelerated rendering, no frame copying
✅ **Memory:** 200MB less usage (no FrameCache)
✅ **Maintainable:** VLC handles video complexity, we handle business logic
✅ **Robust:** Position-based loop prevention works on any CPU speed

## Trade-offs

⚠️ **macOS dev:** Requires Rosetta 2 (x64 emulation)
⚠️ **First frame:** Shows black until Play clicked (standard VLC behavior)
⚠️ **Dependency:** Requires system VLC installation

## Future Considerations

- Windows ARM64 LibVLC support should work natively
- First frame display would require FFMpegCore thumbnail overlay
- Export functionality unchanged (still uses FFMpegCore)
