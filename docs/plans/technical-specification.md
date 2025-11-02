# Bref - Technical Specification

**Date:** 2025-11-01
**Version:** 1.0 - MVP Implementation
**Status:** Approved

## Technology Stack

### Core Framework

**Avalonia UI 11.x**
- NuGet: `Avalonia` (11.0.0+)
- Cross-platform UI framework
- GPU-accelerated rendering (Skia on Windows)
- XAML-based UI definition

**Runtime**
- .NET 8.0 SDK
- Target Framework: net8.0-windows10.0.19041.0
- C# 12 language features

### Dependencies

```xml
<ItemGroup>
  <!-- Avalonia UI -->
  <PackageReference Include="Avalonia" Version="11.0.*" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.*" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.*" />
  <PackageReference Include="Avalonia.Fonts.Inter" Version="11.0.*" />

  <!-- MVVM -->
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.*" />

  <!-- FFmpeg Integration -->
  <PackageReference Include="FFmpeg.AutoGen" Version="7.0.*" />
  <PackageReference Include="FFMpegCore" Version="5.1.*" />

  <!-- Audio Processing -->
  <PackageReference Include="NAudio" Version="2.2.*" />

  <!-- Utilities -->
  <PackageReference Include="Serilog" Version="3.1.*" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.*" />
</ItemGroup>
```

### FFmpeg Binaries

**Distribution:**
- Bundled with application
- Windows 64-bit builds from ffmpeg.org
- Version: 7.0+ (includes hardware acceleration)

**Required Libraries:**
- `avcodec-61.dll`
- `avformat-61.dll`
- `avutil-59.dll`
- `swscale-8.dll`
- `swresample-5.dll`

**Size:** ~50-80 MB total

---

## Project Structure

```
Bref/
├── Bref.sln
├── src/
│   ├── Bref/                          # Main application
│   │   ├── App.axaml                  # Application definition
│   │   ├── App.axaml.cs
│   │   ├── Program.cs                 # Entry point
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml       # Main UI
│   │   │   ├── MainWindow.axaml.cs
│   │   │   └── Controls/
│   │   │       ├── TimelineControl.axaml      # Custom timeline
│   │   │       ├── VideoPlayerControl.axaml   # Video preview
│   │   │       └── WaveformControl.axaml      # Waveform display
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs       # Main window VM
│   │   │   ├── TimelineViewModel.cs
│   │   │   └── VideoPlayerViewModel.cs
│   │   ├── Models/
│   │   │   ├── VideoProject.cs
│   │   │   ├── VideoMetadata.cs
│   │   │   ├── SegmentList.cs
│   │   │   ├── VideoSegment.cs
│   │   │   ├── EditHistory.cs
│   │   │   └── TimelineSelection.cs
│   │   ├── Services/
│   │   │   ├── VideoService.cs
│   │   │   ├── SegmentManager.cs
│   │   │   ├── PlaybackEngine.cs
│   │   │   ├── ExportService.cs
│   │   │   ├── FrameCache.cs
│   │   │   ├── WaveformGenerator.cs
│   │   │   └── ThumbnailGenerator.cs
│   │   ├── FFmpeg/
│   │   │   ├── FFmpegWrapper.cs
│   │   │   ├── HardwareAccelerationDetector.cs
│   │   │   ├── FrameDecoder.cs
│   │   │   └── VideoEncoder.cs
│   │   └── Utilities/
│   │       ├── LRUCache.cs
│   │       └── TimeSpanExtensions.cs
│   └── Bref.Tests/                    # Unit tests
│       ├── Models/
│       ├── Services/
│       └── FFmpeg/
├── docs/
│   ├── plans/                         # Design documents
│   └── api/                           # API documentation
├── assets/
│   ├── icons/                         # Application icons
│   └── ffmpeg/                        # FFmpeg binaries
└── README.md
```

---

## Core Data Models

### VideoProject.cs

```csharp
using System;

namespace Bref.Models
{
    /// <summary>
    /// Represents a complete video editing session
    /// </summary>
    public class VideoProject
    {
        /// <summary>
        /// Absolute path to source video file
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// Metadata about the source video
        /// </summary>
        public VideoMetadata Metadata { get; set; }

        /// <summary>
        /// List of kept segments (virtual timeline)
        /// </summary>
        public SegmentList Segments { get; set; }

        /// <summary>
        /// Undo/redo history for this session
        /// </summary>
        public EditHistory History { get; set; }

        /// <summary>
        /// Last playhead position (for session restore)
        /// </summary>
        public TimeSpan LastPlayheadPosition { get; set; }

        /// <summary>
        /// Project creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime ModifiedAt { get; set; }
    }
}
```

### VideoMetadata.cs

```csharp
using System;

namespace Bref.Models
{
    /// <summary>
    /// Technical metadata about video file
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>
        /// Total duration of source video
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Video width in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Video height in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Frame rate (e.g., 30.0, 29.97, 60.0)
        /// </summary>
        public double FrameRate { get; set; }

        /// <summary>
        /// Video codec (must be "h264" for MVP)
        /// </summary>
        public string VideoCodec { get; set; }

        /// <summary>
        /// Video bitrate in bits per second
        /// </summary>
        public long VideoBitrate { get; set; }

        /// <summary>
        /// Whether video has audio track
        /// </summary>
        public bool HasAudio { get; set; }

        /// <summary>
        /// Audio codec (e.g., "aac", "mp3")
        /// </summary>
        public string AudioCodec { get; set; }

        /// <summary>
        /// Audio bitrate in bits per second
        /// </summary>
        public long AudioBitrate { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Aspect ratio (e.g., "16:9", "4:3")
        /// </summary>
        public string AspectRatio => $"{Width}:{Height}";

        /// <summary>
        /// Frame interval duration (time between frames)
        /// </summary>
        public TimeSpan FrameInterval => TimeSpan.FromSeconds(1.0 / FrameRate);
    }
}
```

### SegmentList.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bref.Models
{
    /// <summary>
    /// Manages the virtual timeline of kept segments
    /// </summary>
    public class SegmentList
    {
        /// <summary>
        /// Ordered list of kept video segments (non-deleted portions)
        /// Invariant: Segments are non-overlapping and sorted by SourceStart
        /// </summary>
        public List<VideoSegment> KeptSegments { get; set; } = new();

        /// <summary>
        /// Total duration of all kept segments (virtual timeline length)
        /// </summary>
        public TimeSpan TotalDuration =>
            TimeSpan.FromSeconds(KeptSegments.Sum(s => s.Duration.TotalSeconds));

        /// <summary>
        /// Number of segments in virtual timeline
        /// </summary>
        public int SegmentCount => KeptSegments.Count;

        /// <summary>
        /// Remove a segment from the virtual timeline
        /// </summary>
        /// <param name="virtualStart">Start time in virtual timeline</param>
        /// <param name="virtualEnd">End time in virtual timeline</param>
        public void DeleteSegment(TimeSpan virtualStart, TimeSpan virtualEnd)
        {
            if (virtualStart >= virtualEnd)
                throw new ArgumentException("Start must be before end");

            if (virtualEnd > TotalDuration)
                throw new ArgumentException("End exceeds virtual duration");

            // Convert virtual times to source times
            var sourceStart = VirtualToSourceTime(virtualStart);
            var sourceEnd = VirtualToSourceTime(virtualEnd);

            // Find affected segments and split/remove them
            var newSegments = new List<VideoSegment>();

            foreach (var segment in KeptSegments)
            {
                if (segment.SourceEnd <= sourceStart || segment.SourceStart >= sourceEnd)
                {
                    // Segment not affected - keep as is
                    newSegments.Add(segment);
                }
                else if (segment.SourceStart < sourceStart && segment.SourceEnd > sourceEnd)
                {
                    // Deletion is in middle of segment - split into two
                    newSegments.Add(new VideoSegment
                    {
                        SourceStart = segment.SourceStart,
                        SourceEnd = sourceStart
                    });
                    newSegments.Add(new VideoSegment
                    {
                        SourceStart = sourceEnd,
                        SourceEnd = segment.SourceEnd
                    });
                }
                else if (segment.SourceStart < sourceStart && segment.SourceEnd > sourceStart)
                {
                    // Deletion overlaps end of segment - trim end
                    newSegments.Add(new VideoSegment
                    {
                        SourceStart = segment.SourceStart,
                        SourceEnd = sourceStart
                    });
                }
                else if (segment.SourceStart < sourceEnd && segment.SourceEnd > sourceEnd)
                {
                    // Deletion overlaps start of segment - trim start
                    newSegments.Add(new VideoSegment
                    {
                        SourceStart = sourceEnd,
                        SourceEnd = segment.SourceEnd
                    });
                }
                // else: segment completely within deletion - don't add
            }

            KeptSegments = newSegments;
        }

        /// <summary>
        /// Convert virtual timeline position to source file position
        /// </summary>
        public TimeSpan VirtualToSourceTime(TimeSpan virtualTime)
        {
            if (virtualTime < TimeSpan.Zero)
                return TimeSpan.Zero;

            var accumulatedVirtualTime = TimeSpan.Zero;

            foreach (var segment in KeptSegments)
            {
                var segmentDuration = segment.Duration;

                if (accumulatedVirtualTime + segmentDuration >= virtualTime)
                {
                    // Virtual time falls within this segment
                    var offsetInSegment = virtualTime - accumulatedVirtualTime;
                    return segment.SourceStart + offsetInSegment;
                }

                accumulatedVirtualTime += segmentDuration;
            }

            // Beyond all segments - return end of last segment
            return KeptSegments.LastOrDefault()?.SourceEnd ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Convert source file position to virtual timeline position
        /// Returns null if source time is in a deleted region
        /// </summary>
        public TimeSpan? SourceToVirtualTime(TimeSpan sourceTime)
        {
            var accumulatedVirtualTime = TimeSpan.Zero;

            foreach (var segment in KeptSegments)
            {
                if (sourceTime >= segment.SourceStart && sourceTime <= segment.SourceEnd)
                {
                    // Source time is within this kept segment
                    var offsetInSegment = sourceTime - segment.SourceStart;
                    return accumulatedVirtualTime + offsetInSegment;
                }

                if (sourceTime < segment.SourceStart)
                {
                    // Source time is before this segment (in deleted region)
                    return null;
                }

                accumulatedVirtualTime += segment.Duration;
            }

            // Source time is after all segments (in deleted region)
            return null;
        }

        /// <summary>
        /// Deep clone this segment list for undo history
        /// </summary>
        public SegmentList Clone()
        {
            return new SegmentList
            {
                KeptSegments = KeptSegments
                    .Select(s => new VideoSegment
                    {
                        SourceStart = s.SourceStart,
                        SourceEnd = s.SourceEnd
                    })
                    .ToList()
            };
        }
    }
}
```

### VideoSegment.cs

```csharp
using System;

namespace Bref.Models
{
    /// <summary>
    /// Represents a continuous portion of the source video (kept segment)
    /// </summary>
    public class VideoSegment
    {
        /// <summary>
        /// Start position in source video file
        /// </summary>
        public TimeSpan SourceStart { get; set; }

        /// <summary>
        /// End position in source video file
        /// </summary>
        public TimeSpan SourceEnd { get; set; }

        /// <summary>
        /// Duration of this segment
        /// </summary>
        public TimeSpan Duration => SourceEnd - SourceStart;

        /// <summary>
        /// Check if this segment contains a source timestamp
        /// </summary>
        public bool Contains(TimeSpan sourceTime) =>
            sourceTime >= SourceStart && sourceTime <= SourceEnd;
    }
}
```

### EditHistory.cs

```csharp
using System.Collections.Generic;

namespace Bref.Models
{
    /// <summary>
    /// Manages undo/redo stack for segment operations
    /// </summary>
    public class EditHistory
    {
        private Stack<SegmentList> _undoStack = new();
        private Stack<SegmentList> _redoStack = new();

        /// <summary>
        /// Maximum undo history depth (prevent memory issues)
        /// </summary>
        public int MaxHistoryDepth { get; set; } = 50;

        /// <summary>
        /// Can undo?
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Can redo?
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Push current state onto undo stack before making changes
        /// </summary>
        public void PushState(SegmentList currentState)
        {
            _undoStack.Push(currentState.Clone());

            // Clear redo stack on new action
            _redoStack.Clear();

            // Limit history depth
            if (_undoStack.Count > MaxHistoryDepth)
            {
                // Remove oldest entry
                var tempStack = new Stack<SegmentList>();
                for (int i = 0; i < MaxHistoryDepth; i++)
                {
                    tempStack.Push(_undoStack.Pop());
                }
                _undoStack = tempStack;
            }
        }

        /// <summary>
        /// Undo last operation
        /// </summary>
        /// <param name="currentState">Current state to push to redo</param>
        /// <returns>Previous state from undo stack</returns>
        public SegmentList Undo(SegmentList currentState)
        {
            if (!CanUndo)
                return currentState;

            _redoStack.Push(currentState.Clone());
            return _undoStack.Pop();
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        /// <param name="currentState">Current state to push to undo</param>
        /// <returns>Next state from redo stack</returns>
        public SegmentList Redo(SegmentList currentState)
        {
            if (!CanRedo)
                return currentState;

            _undoStack.Push(currentState.Clone());
            return _redoStack.Pop();
        }

        /// <summary>
        /// Clear all history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
```

### TimelineSelection.cs

```csharp
using System;

namespace Bref.Models
{
    /// <summary>
    /// Represents current timeline selection state (UI state)
    /// </summary>
    public class TimelineSelection
    {
        /// <summary>
        /// Selection start time in virtual timeline (null if no selection)
        /// </summary>
        public TimeSpan? SelectionStart { get; set; }

        /// <summary>
        /// Selection end time in virtual timeline (null if no selection)
        /// </summary>
        public TimeSpan? SelectionEnd { get; set; }

        /// <summary>
        /// Does user have an active selection?
        /// </summary>
        public bool HasSelection =>
            SelectionStart.HasValue &&
            SelectionEnd.HasValue &&
            SelectionEnd > SelectionStart;

        /// <summary>
        /// Duration of selection
        /// </summary>
        public TimeSpan SelectionDuration =>
            HasSelection
                ? SelectionEnd.Value - SelectionStart.Value
                : TimeSpan.Zero;

        /// <summary>
        /// Clear current selection
        /// </summary>
        public void Clear()
        {
            SelectionStart = null;
            SelectionEnd = null;
        }

        /// <summary>
        /// Set selection range (ensures start before end)
        /// </summary>
        public void SetSelection(TimeSpan start, TimeSpan end)
        {
            if (start < end)
            {
                SelectionStart = start;
                SelectionEnd = end;
            }
            else
            {
                SelectionStart = end;
                SelectionEnd = start;
            }
        }
    }
}
```

---

## Service Layer Implementation

### SegmentManager.cs

```csharp
using System;
using Bref.Models;

namespace Bref.Services
{
    /// <summary>
    /// Core business logic for managing video segments and edit operations
    /// </summary>
    public class SegmentManager
    {
        private SegmentList _currentSegments;
        private EditHistory _history;

        public SegmentManager()
        {
            _history = new EditHistory();
        }

        /// <summary>
        /// Current segment list (virtual timeline)
        /// </summary>
        public SegmentList CurrentSegments => _currentSegments;

        /// <summary>
        /// Edit history for undo/redo
        /// </summary>
        public EditHistory History => _history;

        /// <summary>
        /// Initialize with full video as single segment
        /// </summary>
        public void Initialize(TimeSpan totalDuration)
        {
            _currentSegments = new SegmentList
            {
                KeptSegments = new List<VideoSegment>
                {
                    new VideoSegment
                    {
                        SourceStart = TimeSpan.Zero,
                        SourceEnd = totalDuration
                    }
                }
            };

            _history.Clear();
        }

        /// <summary>
        /// Delete segment from virtual timeline with undo support
        /// </summary>
        public void DeleteSegment(TimeSpan virtualStart, TimeSpan virtualEnd)
        {
            // Save current state for undo
            _history.PushState(_currentSegments);

            // Perform deletion
            _currentSegments.DeleteSegment(virtualStart, virtualEnd);
        }

        /// <summary>
        /// Undo last deletion
        /// </summary>
        public void Undo()
        {
            if (_history.CanUndo)
            {
                _currentSegments = _history.Undo(_currentSegments);
            }
        }

        /// <summary>
        /// Redo last undone deletion
        /// </summary>
        public void Redo()
        {
            if (_history.CanRedo)
            {
                _currentSegments = _history.Redo(_currentSegments);
            }
        }
    }
}
```

---

## FFmpeg Integration

### HardwareAccelerationDetector.cs

```csharp
using System;
using System.Diagnostics;
using System.Linq;

namespace Bref.FFmpeg
{
    /// <summary>
    /// Detects available hardware video encoders
    /// </summary>
    public static class HardwareAccelerationDetector
    {
        private static string _cachedEncoder = null;

        /// <summary>
        /// Detect best available encoder (cached after first call)
        /// Priority: NVENC (NVIDIA) > Quick Sync (Intel) > AMF (AMD) > Software
        /// </summary>
        public static string DetectBestEncoder()
        {
            if (_cachedEncoder != null)
                return _cachedEncoder;

            // Check in priority order
            if (HasEncoder("h264_nvenc"))
            {
                _cachedEncoder = "h264_nvenc";  // NVIDIA GPU
                return _cachedEncoder;
            }

            if (HasEncoder("h264_qsv"))
            {
                _cachedEncoder = "h264_qsv";    // Intel Quick Sync
                return _cachedEncoder;
            }

            if (HasEncoder("h264_amf"))
            {
                _cachedEncoder = "h264_amf";    // AMD GPU
                return _cachedEncoder;
            }

            // Software fallback (always available)
            _cachedEncoder = "libx264";
            return _cachedEncoder;
        }

        /// <summary>
        /// Check if specific encoder is available
        /// </summary>
        private static bool HasEncoder(string encoderName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = "-encoders",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Check if encoder appears in output
                return output.Contains(encoderName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get friendly name for encoder
        /// </summary>
        public static string GetEncoderDisplayName(string encoderName)
        {
            return encoderName switch
            {
                "h264_nvenc" => "NVENC (NVIDIA)",
                "h264_qsv" => "Quick Sync (Intel)",
                "h264_amf" => "AMF (AMD)",
                "libx264" => "Software (CPU)",
                _ => encoderName
            };
        }
    }
}
```

---

## Performance Optimization

### LRUCache.cs

```csharp
using System;
using System.Collections.Generic;

namespace Bref.Utilities
{
    /// <summary>
    /// Least Recently Used cache with fixed capacity
    /// </summary>
    public class LRUCache<TKey, TValue> where TValue : IDisposable
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;

        public LRUCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// Get value from cache (returns null if not found)
        /// </summary>
        public TValue Get(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                return node.Value.Value;
            }

            return default(TValue);
        }

        /// <summary>
        /// Add or update value in cache
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing entry
                existingNode.Value.Value?.Dispose();
                existingNode.Value.Value = value;

                // Move to front
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // Evict if at capacity
                if (_cache.Count >= _capacity)
                {
                    var lruNode = _lruList.Last;
                    _lruList.RemoveLast();
                    _cache.Remove(lruNode.Value.Key);
                    lruNode.Value.Value?.Dispose();
                }

                // Add new entry
                var newItem = new CacheItem { Key = key, Value = value };
                var newNode = _lruList.AddFirst(newItem);
                _cache[key] = newNode;
            }
        }

        /// <summary>
        /// Clear entire cache
        /// </summary>
        public void Clear()
        {
            foreach (var node in _lruList)
            {
                node.Value?.Dispose();
            }

            _cache.Clear();
            _lruList.Clear();
        }

        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
        }
    }
}
```

---

## Export Implementation

### ExportService.cs Pseudocode

```csharp
public class ExportService
{
    public async Task ExportVideo(
        string sourceFile,
        SegmentList segments,
        string outputFile,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken)
    {
        // 1. Detect best encoder
        var encoder = HardwareAccelerationDetector.DetectBestEncoder();

        // 2. Build FFmpeg filter_complex command
        var filterComplex = BuildSegmentFilter(segments);

        // 3. Build FFmpeg command line
        var arguments = $"-i \"{sourceFile}\" " +
                       $"-filter_complex \"{filterComplex}\" " +
                       $"-c:v {encoder} " +
                       $"-c:a copy " +
                       $"-y \"{outputFile}\"";

        // 4. Execute FFmpeg with progress monitoring
        await ExecuteFFmpegAsync(arguments, progress, cancellationToken);
    }

    private string BuildSegmentFilter(SegmentList segments)
    {
        // Example output for 2 segments:
        // [0:v]trim=start=0:end=5,setpts=PTS-STARTPTS[v0];
        // [0:a]atrim=start=0:end=5,asetpts=PTS-STARTPTS[a0];
        // [0:v]trim=start=10:end=60,setpts=PTS-STARTPTS[v1];
        // [0:a]atrim=start=10:end=60,asetpts=PTS-STARTPTS[a1];
        // [v0][a0][v1][a1]concat=n=2:v=1:a=1[outv][outa]

        var filters = new List<string>();

        for (int i = 0; i < segments.KeptSegments.Count; i++)
        {
            var segment = segments.KeptSegments[i];
            var start = segment.SourceStart.TotalSeconds;
            var end = segment.SourceEnd.TotalSeconds;

            // Video trim
            filters.Add($"[0:v]trim=start={start}:end={end},setpts=PTS-STARTPTS[v{i}]");

            // Audio trim
            filters.Add($"[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS[a{i}]");
        }

        // Concatenate all segments
        var concatInputs = string.Join("",
            Enumerable.Range(0, segments.SegmentCount)
                .Select(i => $"[v{i}][a{i}]"));

        filters.Add($"{concatInputs}concat=n={segments.SegmentCount}:v=1:a=1[outv][outa]");

        return string.Join(";", filters);
    }
}
```

---

## Testing Requirements

### Unit Tests

**SegmentList Tests:**
- Initialize with single segment
- Delete from middle (split into two)
- Delete from start (trim start)
- Delete from end (trim end)
- Delete entire segment
- Multiple deletions
- VirtualToSourceTime conversion
- SourceToVirtualTime conversion
- Edge cases (empty, single frame, etc.)

**EditHistory Tests:**
- Push state
- Undo operation
- Redo operation
- Multiple undo/redo
- History depth limit
- Clear history

**HardwareAccelerationDetector Tests:**
- Detect NVENC (if available)
- Detect Quick Sync (if available)
- Fallback to software
- Cache result

### Integration Tests

**Video Loading:**
- Load valid MP4/H.264 file
- Reject invalid format
- Extract metadata correctly
- Generate waveform
- Generate thumbnails

**Playback:**
- Play single segment
- Play across segment boundary
- Seek within segment
- Seek to segment boundary

**Export:**
- Export single segment
- Export multiple segments
- Hardware encoder selection
- Progress reporting
- Cancel during export

---

## Performance Benchmarks

**Target Metrics:**

| Operation | Target | Measure |
|-----------|--------|---------|
| Metadata extraction | <2s | Time to read video info |
| Waveform generation | <10s | 1-hour video |
| Thumbnail generation | <15s | 1-hour video (720 thumbs) |
| Timeline scrubbing | 60fps | Frame rate during drag |
| Frame stepping | <50ms | Response time per frame |
| Deletion operation | <100ms | UI update time |
| Undo/redo | <50ms | State restoration time |
| Export (NVENC) | 5-10x | Realtime speed |
| Export (software) | 0.5-2x | Realtime speed |
| Memory (idle) | <100MB | With video loaded |
| Memory (peak) | <500MB | During playback |

---

## Configuration

### App Settings (JSON)

```json
{
  "Performance": {
    "FrameCacheSize": 60,
    "ThumbnailInterval": 5,
    "WaveformSamplesPerSecond": 1000,
    "MaxUndoHistory": 50
  },
  "Export": {
    "PreferredEncoder": "auto",
    "TempDirectory": "%TEMP%\\Bref",
    "DefaultOutputDirectory": "%USERPROFILE%\\Videos"
  },
  "UI": {
    "Theme": "Dark",
    "Language": "en-US",
    "RecentFilesLimit": 10
  }
}
```

---

## Logging

**Serilog Configuration:**

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: "logs/bref-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

**Log Levels:**
- Information: User actions (open, delete, export)
- Warning: Performance issues, fallback to software encoding
- Error: FFmpeg errors, file access issues
- Debug: Detailed timing, cache hits/misses

---

## Deployment

### MSIX Packaging

**Package.appxmanifest:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="Bref"
            Publisher="CN=Publisher"
            Version="1.0.0.0" />

  <Properties>
    <DisplayName>Bref - Video Editor</DisplayName>
    <PublisherDisplayName>Publisher Name</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop"
                        MinVersion="10.0.19041.0"
                        MaxVersionTested="10.0.22621.0" />
  </Dependencies>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>

  <Applications>
    <Application Id="Bref" Executable="Bref.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="Bref"
                          Description="Video editing tool for removing unwanted segments"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\Square150x150Logo.png"
                          Square44x44Logo="Assets\Square44x44Logo.png">
      </uap:VisualElements>

      <Extensions>
        <uap3:Extension Category="windows.fileTypeAssociation">
          <uap3:FileTypeAssociation Name="mp4">
            <uap:SupportedFileTypes>
              <uap:FileType>.mp4</uap:FileType>
            </uap:SupportedFileTypes>
          </uap3:FileTypeAssociation>
        </uap3:Extension>

        <uap3:Extension Category="windows.fileTypeAssociation">
          <uap3:FileTypeAssociation Name="bref">
            <uap:SupportedFileTypes>
              <uap:FileType>.bref</uap:FileType>
            </uap:SupportedFileTypes>
          </uap3:FileTypeAssociation>
        </uap3:Extension>
      </Extensions>
    </Application>
  </Applications>
</Package>
```

### Build Process

```bash
# Restore dependencies
dotnet restore

# Build release
dotnet build -c Release

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained true

# Create MSIX package
makeappx pack /d publish/win-x64 /p Bref.msix

# Sign package
signtool sign /fd SHA256 /a Bref.msix
```

---

## Conclusion

This technical specification provides complete implementation details for Bref MVP, covering:
- ✓ Complete data models with C# code
- ✓ Core algorithms (segment management, time conversion)
- ✓ FFmpeg integration strategy
- ✓ Performance optimization (LRU cache)
- ✓ Export implementation (filter_complex)
- ✓ Testing requirements
- ✓ Deployment configuration

**Ready for development with Claude Code assistance.**
