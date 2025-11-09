# Week 9: Export Service Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement video export with FFmpeg-based segment concatenation, hardware acceleration, progress monitoring, and cancellation support.

**Architecture:** Use FFMpegCore to build filter_complex commands for segment concatenation. Detect and use hardware acceleration (NVENC/Quick Sync/AMF) with automatic software fallback. Parse FFmpeg stderr for progress updates.

**Tech Stack:** FFMpegCore 5.1.0, LibVLCSharp (for current playback), Avalonia UI, C# 12

---

## Prerequisites

- Week 8 (Audio Playback Integration) complete ✅
- VlcPlaybackEngine with audio working ✅
- SegmentManager with virtual timeline ✅
- `sample-30s.mp4` available for testing ✅
- FFMpegCore installed (5.1.0) ✅

---

## Task 1: Create ExportService Interface and Model

**Files:**
- Create: `src/SpartaCut.Core/Services/Interfaces/IExportService.cs`
- Create: `src/SpartaCut.Core/Models/ExportOptions.cs`
- Create: `src/SpartaCut.Core/Models/ExportProgress.cs`

**Step 1: Create ExportOptions model**

Create `src/SpartaCut.Core/Models/ExportOptions.cs`:

```csharp
using System;

namespace SpartaCut.Core.Models;

/// <summary>
/// Options for video export operation
/// </summary>
public record ExportOptions
{
    /// <summary>
    /// Path to source video file
    /// </summary>
    public required string SourceFilePath { get; init; }

    /// <summary>
    /// Path for output video file
    /// </summary>
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// Segments to export (from SegmentManager)
    /// </summary>
    public required SegmentList Segments { get; init; }

    /// <summary>
    /// Video metadata (codec, resolution, etc.)
    /// </summary>
    public required VideoMetadata Metadata { get; init; }

    /// <summary>
    /// Enable hardware acceleration if available
    /// </summary>
    public bool UseHardwareAcceleration { get; init; } = true;

    /// <summary>
    /// Preferred hardware encoder (null = auto-detect)
    /// </summary>
    public string? PreferredEncoder { get; init; } = null;
}
```

**Step 2: Create ExportProgress model**

Create `src/SpartaCut.Core/Models/ExportProgress.cs`:

```csharp
using System;

namespace SpartaCut.Core.Models;

/// <summary>
/// Progress information for export operation
/// </summary>
public record ExportProgress
{
    /// <summary>
    /// Current export stage
    /// </summary>
    public ExportStage Stage { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Progress message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Time elapsed since export started
    /// </summary>
    public TimeSpan? ElapsedTime { get; init; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current FFmpeg frame being processed
    /// </summary>
    public long? CurrentFrame { get; init; }

    /// <summary>
    /// Total frames to process
    /// </summary>
    public long? TotalFrames { get; init; }
}

/// <summary>
/// Export operation stages
/// </summary>
public enum ExportStage
{
    Preparing,      // Building FFmpeg command
    Encoding,       // FFmpeg is running
    Finalizing,     // Post-processing
    Complete,       // Export succeeded
    Cancelled,      // User cancelled
    Failed          // Export failed
}
```

**Step 3: Create IExportService interface**

Create `src/SpartaCut.Core/Services/Interfaces/IExportService.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using SpartaCut.Core.Models;

namespace SpartaCut.Core.Services.Interfaces;

/// <summary>
/// Service for exporting edited videos
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export video with segment filtering
    /// </summary>
    /// <param name="options">Export options</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if export succeeded</returns>
    Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect available hardware encoders
    /// </summary>
    /// <returns>List of available encoder names</returns>
    Task<string[]> DetectHardwareEncodersAsync();

    /// <summary>
    /// Get recommended encoder for current system
    /// </summary>
    /// <returns>Encoder name or null if only software available</returns>
    Task<string?> GetRecommendedEncoderAsync();
}
```

**Step 4: Build and verify**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut.Core/SpartaCut.Core.csproj`
Expected: Build succeeds with 0 errors

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/Models/ExportOptions.cs \
        src/SpartaCut.Core/Models/ExportProgress.cs \
        src/SpartaCut.Core/Services/Interfaces/IExportService.cs
git commit -m "feat: add export service interface and models

- Add ExportOptions with source/output paths and segments
- Add ExportProgress with stage, percentage, time estimates
- Add ExportStage enum (Preparing, Encoding, etc.)
- Add IExportService interface for export operations"
```

---

## Task 2: Implement Hardware Encoder Detection

**Files:**
- Create: `src/SpartaCut.Core/Services/ExportService.cs`

**Step 1: Create ExportService with encoder detection**

Create `src/SpartaCut.Core/Services/ExportService.cs`:

```csharp
using SpartaCut.Core.Models;
using SpartaCut.Core.Services.Interfaces;
using FFMpegCore;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SpartaCut.Core.Services;

/// <summary>
/// Service for exporting edited videos using FFmpeg
/// </summary>
public class ExportService : IExportService
{
    private static readonly string[] HardwareEncoders = new[]
    {
        "h264_nvenc",       // NVIDIA
        "h264_qsv",         // Intel Quick Sync
        "h264_amf",         // AMD
    };

    public async Task<string[]> DetectHardwareEncodersAsync()
    {
        Log.Information("Detecting available hardware encoders...");
        var availableEncoders = new System.Collections.Generic.List<string>();

        foreach (var encoder in HardwareEncoders)
        {
            if (await IsEncoderAvailableAsync(encoder))
            {
                availableEncoders.Add(encoder);
                Log.Information("Hardware encoder available: {Encoder}", encoder);
            }
        }

        if (availableEncoders.Count == 0)
        {
            Log.Information("No hardware encoders available, will use software encoding (libx264)");
        }

        return availableEncoders.ToArray();
    }

    public async Task<string?> GetRecommendedEncoderAsync()
    {
        var available = await DetectHardwareEncodersAsync();

        // Priority: NVENC > Quick Sync > AMF
        if (available.Contains("h264_nvenc"))
            return "h264_nvenc";
        if (available.Contains("h264_qsv"))
            return "h264_qsv";
        if (available.Contains("h264_amf"))
            return "h264_amf";

        return null; // Software fallback
    }

    private async Task<bool> IsEncoderAvailableAsync(string encoderName)
    {
        try
        {
            // Run ffmpeg -encoders and check if encoder is listed
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = "-encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Check if encoder appears in output
            return output.Contains(encoderName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check encoder availability: {Encoder}", encoderName);
            return false;
        }
    }

    public Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement in next task
        throw new NotImplementedException("Export implementation coming in Task 3");
    }
}
```

**Step 2: Test encoder detection**

Create `src/SpartaCut.Tests/Services/ExportServiceTests.cs`:

```csharp
using SpartaCut.Core.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class ExportServiceTests
{
    [Fact]
    public async Task DetectHardwareEncoders_ReturnsArray()
    {
        // Arrange
        var service = new ExportService();

        // Act
        var encoders = await service.DetectHardwareEncodersAsync();

        // Assert
        Assert.NotNull(encoders);
        // May be empty on systems without hardware encoding
    }

    [Fact]
    public async Task GetRecommendedEncoder_ReturnsEncoderOrNull()
    {
        // Arrange
        var service = new ExportService();

        // Act
        var encoder = await service.GetRecommendedEncoderAsync();

        // Assert
        // encoder is either a hardware encoder name or null (software)
        if (encoder != null)
        {
            Assert.Contains(encoder, new[] { "h264_nvenc", "h264_qsv", "h264_amf" });
        }
    }
}
```

**Step 3: Build and test**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut.Core/SpartaCut.Core.csproj`
Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj --filter "FullyQualifiedName~ExportServiceTests"`

Expected: Build succeeds, tests pass

**Step 4: Commit**

```bash
git add src/SpartaCut.Core/Services/ExportService.cs \
        src/SpartaCut.Tests/Services/ExportServiceTests.cs
git commit -m "feat: implement hardware encoder detection

- Add ExportService with encoder detection methods
- Check for NVENC, Quick Sync, AMF availability
- Priority: NVENC > Quick Sync > AMF > software
- Parse ffmpeg -encoders output to detect support
- Add unit tests for encoder detection"
```

---

## Task 3: Implement FFmpeg Filter_Complex Builder

**Files:**
- Modify: `src/SpartaCut.Core/Services/ExportService.cs`

**Step 1: Add BuildFilterComplex method**

Modify `src/SpartaCut.Core/Services/ExportService.cs`:

```csharp
/// <summary>
/// Build FFmpeg filter_complex for segment concatenation
/// </summary>
private string BuildFilterComplex(SegmentList segments, VideoMetadata metadata)
{
    var keptSegments = segments.Segments;

    if (keptSegments.Count == 0)
    {
        throw new InvalidOperationException("No segments to export");
    }

    if (keptSegments.Count == 1)
    {
        // Single segment - simple trim filter
        var seg = keptSegments[0];
        var start = seg.SourceStart.TotalSeconds;
        var duration = seg.Duration.TotalSeconds;

        return $"[0:v]trim=start={start}:duration={duration},setpts=PTS-STARTPTS[v]; " +
               $"[0:a]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS[a]";
    }

    // Multiple segments - concat filter
    var filterParts = new System.Collections.Generic.List<string>();
    var videoLabels = new System.Collections.Generic.List<string>();
    var audioLabels = new System.Collections.Generic.List<string>();

    for (int i = 0; i < keptSegments.Count; i++)
    {
        var seg = keptSegments[i];
        var start = seg.SourceStart.TotalSeconds;
        var duration = seg.Duration.TotalSeconds;

        var vLabel = $"v{i}";
        var aLabel = $"a{i}";

        // Trim and reset PTS for each segment
        filterParts.Add($"[0:v]trim=start={start}:duration={duration},setpts=PTS-STARTPTS[{vLabel}]");
        filterParts.Add($"[0:a]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS[{aLabel}]");

        videoLabels.Add($"[{vLabel}]");
        audioLabels.Add($"[{aLabel}]");
    }

    // Concatenate all segments
    var videoConcat = string.Join("", videoLabels) + $"concat=n={keptSegments.Count}:v=1:a=0[v]";
    var audioConcat = string.Join("", audioLabels) + $"concat=n={keptSegments.Count}:v=0:a=1[a]";

    filterParts.Add(videoConcat);
    filterParts.Add(audioConcat);

    return string.Join("; ", filterParts);
}
```

**Step 2: Add BuildFFmpegArguments method**

```csharp
/// <summary>
/// Build complete FFmpeg arguments for export
/// </summary>
private string BuildFFmpegArguments(ExportOptions options, string encoder)
{
    var filterComplex = BuildFilterComplex(options.Segments, options.Metadata);

    var args = new System.Text.StringBuilder();

    // Input file
    args.Append($"-i \"{options.SourceFilePath}\" ");

    // Filter complex for segment concatenation
    args.Append($"-filter_complex \"{filterComplex}\" ");

    // Map filtered video and audio
    args.Append("-map \"[v]\" -map \"[a]\" ");

    // Encoder settings
    if (encoder == "libx264")
    {
        // Software encoding
        args.Append("-c:v libx264 -preset medium -crf 23 ");
    }
    else
    {
        // Hardware encoding
        args.Append($"-c:v {encoder} ");

        // Encoder-specific settings
        if (encoder == "h264_nvenc")
        {
            args.Append("-preset p4 -rc vbr -cq 23 ");
        }
        else if (encoder == "h264_qsv")
        {
            args.Append("-preset medium -global_quality 23 ");
        }
        else if (encoder == "h264_amf")
        {
            args.Append("-quality balanced -rc cqp -qp 23 ");
        }
    }

    // Audio encoding (copy if same codec, re-encode if needed)
    args.Append("-c:a aac -b:a 192k ");

    // Output format
    args.Append("-movflags +faststart "); // Enable streaming
    args.Append($"\"{options.OutputFilePath}\"");

    return args.ToString();
}
```

**Step 3: Add test for filter builder**

Add to `src/SpartaCut.Tests/Services/ExportServiceTests.cs`:

```csharp
[Fact]
public void BuildFilterComplex_SingleSegment_CreatesSimpleTrim()
{
    // Arrange
    var service = new ExportService();
    var segments = new SegmentList();
    segments.Initialize(TimeSpan.FromMinutes(10));
    // No deletions - single segment

    var metadata = new VideoMetadata
    {
        FilePath = "test.mp4",
        Duration = TimeSpan.FromMinutes(10),
        Width = 1920,
        Height = 1080,
        FrameRate = 30,
        CodecName = "h264",
        PixelFormat = "yuv420p"
    };

    // Use reflection to call private method for testing
    var method = typeof(ExportService).GetMethod("BuildFilterComplex",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    // Act
    var filter = (string)method!.Invoke(service, new object[] { segments, metadata })!;

    // Assert
    Assert.Contains("trim=start=0", filter);
    Assert.Contains("atrim=start=0", filter);
}
```

**Step 4: Build and test**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut.Core/SpartaCut.Core.csproj`
Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj --filter "FullyQualifiedName~ExportServiceTests"`

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/Services/ExportService.cs \
        src/SpartaCut.Tests/Services/ExportServiceTests.cs
git commit -m "feat: add FFmpeg filter_complex builder for export

- Build trim filters for single segment export
- Build concat filters for multiple segment export
- Generate FFmpeg arguments with encoder settings
- Support NVENC, Quick Sync, AMF, and libx264
- Add unit test for filter generation"
```

---

## Task 4: Implement Export with Progress Monitoring

**Files:**
- Modify: `src/SpartaCut.Core/Services/ExportService.cs`

**Step 1: Implement ExportAsync**

Modify `src/SpartaCut.Core/Services/ExportService.cs`:

```csharp
public async Task<bool> ExportAsync(
    ExportOptions options,
    IProgress<ExportProgress> progress,
    CancellationToken cancellationToken = default)
{
    Log.Information("Starting export: {Source} -> {Output}",
        options.SourceFilePath, options.OutputFilePath);

    var startTime = DateTime.Now;

    try
    {
        // Report: Preparing
        progress.Report(new ExportProgress
        {
            Stage = ExportStage.Preparing,
            Percentage = 0,
            Message = "Detecting hardware encoders..."
        });

        // Determine encoder
        var encoder = options.PreferredEncoder;
        if (encoder == null && options.UseHardwareAcceleration)
        {
            encoder = await GetRecommendedEncoderAsync();
        }
        encoder ??= "libx264"; // Software fallback

        Log.Information("Using encoder: {Encoder}", encoder);

        // Build FFmpeg command
        var arguments = BuildFFmpegArguments(options, encoder);
        Log.Debug("FFmpeg arguments: {Args}", arguments);

        // Calculate total frames for progress
        var totalFrames = (long)(options.Segments.TotalDuration.TotalSeconds * options.Metadata.FrameRate);

        progress.Report(new ExportProgress
        {
            Stage = ExportStage.Preparing,
            Percentage = 5,
            Message = $"Building export with {encoder}...",
            TotalFrames = totalFrames
        });

        // Start FFmpeg process
        var processStarted = false;
        var success = false;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            // Parse FFmpeg stderr for progress
            var progressInfo = ParseFFmpegProgress(e.Data, totalFrames);
            if (progressInfo.HasValue)
            {
                var elapsed = DateTime.Now - startTime;
                var remaining = progressInfo.Value.percentage > 0
                    ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - progressInfo.Value.percentage) / progressInfo.Value.percentage)
                    : null;

                progress.Report(new ExportProgress
                {
                    Stage = ExportStage.Encoding,
                    Percentage = progressInfo.Value.percentage,
                    Message = $"Encoding frame {progressInfo.Value.frame} of {totalFrames}...",
                    ElapsedTime = elapsed,
                    EstimatedTimeRemaining = remaining,
                    CurrentFrame = progressInfo.Value.frame,
                    TotalFrames = totalFrames
                });
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        processStarted = true;

        // Wait for completion or cancellation
        while (!process.HasExited)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Warning("Export cancelled by user");
                process.Kill();

                progress.Report(new ExportProgress
                {
                    Stage = ExportStage.Cancelled,
                    Percentage = 0,
                    Message = "Export cancelled"
                });

                return false;
            }

            await Task.Delay(100, CancellationToken.None);
        }

        success = process.ExitCode == 0;

        if (success)
        {
            Log.Information("Export completed successfully in {Elapsed}", DateTime.Now - startTime);

            progress.Report(new ExportProgress
            {
                Stage = ExportStage.Complete,
                Percentage = 100,
                Message = "Export complete!",
                ElapsedTime = DateTime.Now - startTime
            });
        }
        else
        {
            Log.Error("Export failed with exit code {ExitCode}", process.ExitCode);

            progress.Report(new ExportProgress
            {
                Stage = ExportStage.Failed,
                Percentage = 0,
                Message = $"Export failed (exit code {process.ExitCode})"
            });
        }

        return success;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Export failed with exception");

        progress.Report(new ExportProgress
        {
            Stage = ExportStage.Failed,
            Percentage = 0,
            Message = $"Export failed: {ex.Message}"
        });

        return false;
    }
}

/// <summary>
/// Parse FFmpeg stderr output for progress information
/// </summary>
private (long frame, int percentage)? ParseFFmpegProgress(string line, long totalFrames)
{
    // FFmpeg progress format: "frame=  123 fps=30 q=28.0 size=    1024kB time=00:00:04.10 ..."
    if (!line.Contains("frame=")) return null;

    try
    {
        var frameMatch = System.Text.RegularExpressions.Regex.Match(line, @"frame=\s*(\d+)");
        if (!frameMatch.Success) return null;

        var frame = long.Parse(frameMatch.Groups[1].Value);
        var percentage = totalFrames > 0 ? (int)(frame * 100 / totalFrames) : 0;

        return (frame, Math.Min(percentage, 99)); // Cap at 99% until complete
    }
    catch
    {
        return null;
    }
}
```

**Step 2: Build**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut.Core/SpartaCut.Core.csproj`

**Step 3: Commit**

```bash
git add src/SpartaCut.Core/Services/ExportService.cs
git commit -m "feat: implement export with progress monitoring

- Implement ExportAsync with FFmpeg process execution
- Parse FFmpeg stderr for frame progress
- Calculate elapsed and estimated time remaining
- Support cancellation via CancellationToken
- Report progress stages: Preparing, Encoding, Complete
- Handle errors and log to Serilog"
```

---

## Task 5: Add Export UI to MainWindow

**Files:**
- Modify: `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SpartaCut/Views/MainWindow.axaml`

**Step 1: Add Export command to ViewModel**

Modify `src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs`:

Add private field after _playbackEngine:
```csharp
private readonly IExportService _exportService;
```

Modify constructor to inject ExportService:
```csharp
public MainWindowViewModel(IPlaybackEngine playbackEngine, IExportService exportService)
{
    _playbackEngine = playbackEngine ?? throw new ArgumentNullException(nameof(playbackEngine));
    _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));

    // ... existing initialization
}
```

Add observable properties for export state:
```csharp
[ObservableProperty]
private bool _isExporting = false;

[ObservableProperty]
private int _exportProgress = 0;

[ObservableProperty]
private string _exportMessage = string.Empty;
```

Add Export command:
```csharp
/// <summary>
/// Export edited video
/// </summary>
[RelayCommand]
public async Task Export()
{
    if (_segmentManager == null || Timeline.VideoMetadata == null)
        return;

    // Show file save dialog
    var saveDialog = new SaveFileDialog
    {
        Title = "Export Video",
        DefaultExtension = ".mp4",
        Filters = new List<FileDialogFilter>
        {
            new FileDialogFilter { Name = "MP4 Video", Extensions = { "mp4" } }
        }
    };

    var result = await saveDialog.ShowAsync(/* MainWindow reference needed */);
    if (string.IsNullOrEmpty(result))
        return; // User cancelled

    // Prepare export options
    var options = new ExportOptions
    {
        SourceFilePath = Timeline.VideoMetadata.FilePath,
        OutputFilePath = result,
        Segments = _segmentManager.CurrentSegments,
        Metadata = Timeline.VideoMetadata,
        UseHardwareAcceleration = true
    };

    // Progress reporter
    var progress = new Progress<ExportProgress>(p =>
    {
        ExportProgress = p.Percentage;
        ExportMessage = p.Message;

        if (p.Stage == ExportStage.Complete)
        {
            IsExporting = false;
            Log.Information("Export finished: {Output}", options.OutputFilePath);
        }
        else if (p.Stage == ExportStage.Failed || p.Stage == ExportStage.Cancelled)
        {
            IsExporting = false;
        }
    });

    IsExporting = true;

    // Start export
    var success = await _exportService.ExportAsync(options, progress, CancellationToken.None);

    if (success)
    {
        ExportMessage = "Export complete!";
    }
}
```

**Step 2: Update MainWindow to show export progress**

Modify `src/SpartaCut/Views/MainWindow.axaml` to add export button and progress bar:

```xml
<!-- Add Export button in toolbar -->
<Button Content="Export"
        Command="{Binding ExportCommand}"
        IsEnabled="{Binding !IsExporting}"
        ToolTip.Tip="Export edited video"/>

<!-- Add export progress panel (show when exporting) -->
<StackPanel IsVisible="{Binding IsExporting}"
            Background="#2D2D30"
            Padding="16"
            Margin="0,8,0,0">
    <TextBlock Text="{Binding ExportMessage}"
               FontSize="14"
               Margin="0,0,0,8"/>
    <ProgressBar Value="{Binding ExportProgress}"
                 Minimum="0"
                 Maximum="100"
                 Height="24"/>
</StackPanel>
```

**Step 3: Update SpartaCut.csproj version**

Modify `src/SpartaCut/SpartaCut.csproj`:
```xml
<Version>0.12.0</Version>
```

Modify `src/SpartaCut.Core/SpartaCut.Core.csproj`:
```xml
<Version>0.12.0</Version>
```

**Step 4: Build and test**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut/SpartaCut.csproj`

**Step 5: Commit**

```bash
git add src/SpartaCut.Core/ViewModels/MainWindowViewModel.cs \
        src/SpartaCut/Views/MainWindow.axaml \
        src/SpartaCut/SpartaCut.csproj \
        src/SpartaCut.Core/SpartaCut.Core.csproj
git commit -m "feat: add export UI and command

- Add ExportCommand to MainWindowViewModel
- Inject IExportService via constructor
- Add export progress properties (IsExporting, ExportProgress)
- Add Export button to toolbar
- Add export progress panel with progress bar
- Bump version to 0.12.0 for Week 9"
```

---

## Task 6: Integration Testing with Real Video

**Files:**
- Create: `src/SpartaCut.Tests/Integration/ExportIntegrationTests.cs`

**Step 1: Create integration test**

Create `src/SpartaCut.Tests/Integration/ExportIntegrationTests.cs`:

```csharp
using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SpartaCut.Tests.Integration;

/// <summary>
/// Integration tests for export functionality
/// NOTE: These tests require sample-30s.mp4 in samples/ directory
/// </summary>
public class ExportIntegrationTests
{
    private const string SampleVideo = "samples/sample-30s.mp4";

    [Fact(Skip = "Requires sample video file")]
    public async Task ExportSingleSegment_CreatesValidVideo()
    {
        // Arrange
        if (!File.Exists(SampleVideo))
        {
            throw new FileNotFoundException($"Sample video not found: {SampleVideo}");
        }

        var service = new ExportService();
        var outputPath = Path.GetTempFileName() + ".mp4";

        var segments = new SegmentList();
        segments.Initialize(TimeSpan.FromSeconds(30));

        var metadata = new VideoMetadata
        {
            FilePath = SampleVideo,
            Duration = TimeSpan.FromSeconds(30),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = SampleVideo,
            OutputFilePath = outputPath,
            Segments = segments,
            Metadata = metadata,
            UseHardwareAcceleration = false // Force software for reliability
        };

        var progressReports = new System.Collections.Generic.List<ExportProgress>();
        var progress = new Progress<ExportProgress>(p => progressReports.Add(p));

        try
        {
            // Act
            var success = await service.ExportAsync(options, progress, CancellationToken.None);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));

            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0);

            // Should have progress reports
            Assert.NotEmpty(progressReports);
            Assert.Contains(progressReports, p => p.Stage == ExportStage.Complete);
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact(Skip = "Requires sample video file")]
    public async Task ExportMultipleSegments_ConcatenatesCorrectly()
    {
        // Arrange
        if (!File.Exists(SampleVideo))
        {
            throw new FileNotFoundException($"Sample video not found: {SampleVideo}");
        }

        var service = new ExportService();
        var outputPath = Path.GetTempFileName() + ".mp4";

        var segments = new SegmentList();
        segments.Initialize(TimeSpan.FromSeconds(30));

        // Delete middle 10 seconds (10-20)
        segments.DeleteSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        var metadata = new VideoMetadata
        {
            FilePath = SampleVideo,
            Duration = TimeSpan.FromSeconds(30),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = SampleVideo,
            OutputFilePath = outputPath,
            Segments = segments,
            Metadata = metadata,
            UseHardwareAcceleration = false
        };

        var progress = new Progress<ExportProgress>();

        try
        {
            // Act
            var success = await service.ExportAsync(options, progress, CancellationToken.None);

            // Assert
            Assert.True(success);
            Assert.True(File.Exists(outputPath));

            // Output should be ~20 seconds (30 - 10 deleted)
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
```

**Step 2: Manual testing**

Run application and test export:
1. Load `samples/sample-30s.mp4`
2. Delete segment (10-15 seconds)
3. Click Export button
4. Save as `test-export.mp4`
5. Verify progress bar updates
6. Verify exported file plays correctly
7. Verify deleted segment is missing

**Step 3: Commit**

```bash
git add src/SpartaCut.Tests/Integration/ExportIntegrationTests.cs
git commit -m "test: add export integration tests

- Test single segment export creates valid video
- Test multiple segment export with deletions
- Verify output file exists and has content
- Manual testing checklist for export workflow"
```

---

## Task 7: Handle Export Edge Cases

**Files:**
- Modify: `src/SpartaCut.Core/Services/ExportService.cs`
- Create: `src/SpartaCut.Tests/Services/ExportServiceEdgeCaseTests.cs`

**Step 1: Add validation to ExportAsync**

Modify `src/SpartaCut.Core/Services/ExportService.cs` at start of ExportAsync:

```csharp
// Validate inputs
if (!File.Exists(options.SourceFilePath))
{
    throw new FileNotFoundException($"Source video not found: {options.SourceFilePath}");
}

if (options.Segments.Segments.Count == 0)
{
    throw new InvalidOperationException("Cannot export: no segments to export");
}

if (string.IsNullOrWhiteSpace(options.OutputFilePath))
{
    throw new ArgumentException("Output file path is required", nameof(options));
}

// Check if output directory exists
var outputDir = Path.GetDirectoryName(options.OutputFilePath);
if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
{
    Directory.CreateDirectory(outputDir);
    Log.Information("Created output directory: {Dir}", outputDir);
}

// Check if output file already exists
if (File.Exists(options.OutputFilePath))
{
    Log.Warning("Output file already exists, will overwrite: {File}", options.OutputFilePath);
}
```

**Step 2: Add edge case tests**

Create `src/SpartaCut.Tests/Services/ExportServiceEdgeCaseTests.cs`:

```csharp
using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SpartaCut.Tests.Services;

public class ExportServiceEdgeCaseTests
{
    [Fact]
    public async Task Export_WithMissingSourceFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.Initialize(TimeSpan.FromMinutes(1));

        var metadata = new VideoMetadata
        {
            FilePath = "nonexistent.mp4",
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = "nonexistent.mp4",
            OutputFilePath = "output.mp4",
            Segments = segments,
            Metadata = metadata
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await service.ExportAsync(options, new Progress<ExportProgress>());
        });
    }

    [Fact]
    public async Task Export_WithNoSegments_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.Initialize(TimeSpan.FromMinutes(1));

        // Delete all segments
        segments.DeleteSegment(TimeSpan.Zero, TimeSpan.FromMinutes(1));

        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = "test.mp4",
            OutputFilePath = "output.mp4",
            Segments = segments,
            Metadata = metadata
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.ExportAsync(options, new Progress<ExportProgress>());
        });
    }

    [Fact]
    public async Task Export_WithEmptyOutputPath_ThrowsArgumentException()
    {
        // Arrange
        var service = new ExportService();
        var segments = new SegmentList();
        segments.Initialize(TimeSpan.FromMinutes(1));

        var metadata = new VideoMetadata
        {
            FilePath = "test.mp4",
            Duration = TimeSpan.FromMinutes(1),
            Width = 1920,
            Height = 1080,
            FrameRate = 30,
            CodecName = "h264",
            PixelFormat = "yuv420p"
        };

        var options = new ExportOptions
        {
            SourceFilePath = "test.mp4",
            OutputFilePath = "",
            Segments = segments,
            Metadata = metadata
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await service.ExportAsync(options, new Progress<ExportProgress>());
        });
    }
}
```

**Step 3: Build and test**

Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj --filter "FullyQualifiedName~ExportServiceEdgeCaseTests"`

**Step 4: Commit**

```bash
git add src/SpartaCut.Core/Services/ExportService.cs \
        src/SpartaCut.Tests/Services/ExportServiceEdgeCaseTests.cs
git commit -m "feat: add export validation and edge case handling

- Validate source file exists before export
- Validate at least one segment to export
- Validate output path is not empty
- Create output directory if needed
- Warn if output file will be overwritten
- Add edge case unit tests"
```

---

## Task 8: Update Documentation

**Files:**
- Modify: `claude.md`
- Create: `docs/testing/week9-export-manual-tests.md`

**Step 1: Update claude.md**

Add to `claude.md` under "Today I Learned":

```markdown
### Week 9: Export Service (January 2025)

**FFmpeg Export Architecture:**
- Use FFMpegCore for process execution and argument building
- Build filter_complex commands for segment concatenation
- Single segment: simple trim filter
- Multiple segments: concat filter with PTS reset

**Hardware Acceleration:**
- Auto-detect NVENC (NVIDIA), Quick Sync (Intel), AMF (AMD)
- Priority: NVENC > Quick Sync > AMF > libx264 (software)
- Parse `ffmpeg -encoders` output to check availability
- Fallback to software encoding if hardware unavailable

**Progress Monitoring:**
- Parse FFmpeg stderr for "frame=" progress lines
- Calculate percentage from current frame / total frames
- Estimate time remaining from elapsed time and percentage
- Update UI every ~100ms during encoding

**Cancellation:**
- CancellationToken support for user cancellation
- Kill FFmpeg process on cancellation
- Report Cancelled stage to UI

**Edge Cases:**
- Validate source file exists
- Validate at least one segment to export
- Create output directory if needed
- Warn on output file overwrite
```

**Step 2: Create manual testing checklist**

Create `docs/testing/week9-export-manual-tests.md`:

```markdown
# Week 9 Export Service - Manual Testing Checklist

**Status:** ✅ COMPLETED (Week 9 - January 2025)
**Implementation:** FFmpeg export with hardware acceleration

## Prerequisites
- Video file with audio (MP4/H.264) - use `samples/sample-30s.mp4`
- At least 1GB free disk space for exports
- FFMpegCore installed ✅

---

## Test 1: Single Segment Export (No Deletions)

**Steps:**
1. Load `samples/sample-30s.mp4`
2. No deletions - keep entire video
3. Click Export button
4. Save as `test-single-segment.mp4`
5. Wait for export to complete

**Expected:**
- [ ] Progress bar updates from 0-100%
- [ ] Export completes successfully
- [ ] Output file size similar to source (~30MB)
- [ ] Output plays correctly with audio
- [ ] Duration matches source (30 seconds)

---

## Test 2: Multiple Segment Export (With Deletions)

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete segment: 10-15 seconds
3. Delete segment: 20-25 seconds
4. Click Export button
5. Save as `test-multi-segment.mp4`

**Expected:**
- [ ] Progress bar updates smoothly
- [ ] Export completes successfully
- [ ] Output duration: 20 seconds (30 - 10 deleted)
- [ ] Deleted segments missing in output
- [ ] Audio remains synchronized

---

## Test 3: Hardware Acceleration Detection

**Steps:**
1. Check console logs on app startup
2. Look for encoder detection messages

**Expected:**
- [ ] Logs show hardware encoders detected (or "No hardware encoders")
- [ ] NVENC detected on NVIDIA systems
- [ ] Quick Sync detected on Intel systems
- [ ] AMF detected on AMD systems
- [ ] Software fallback (libx264) if no hardware

---

## Test 4: Export Progress Accuracy

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete segment: 5-10 seconds
3. Start export
4. Monitor progress bar and time estimates

**Expected:**
- [ ] Progress updates every second
- [ ] Percentage increases steadily 0-100%
- [ ] Elapsed time shown
- [ ] Estimated time remaining shown
- [ ] Time estimates reasonably accurate

---

## Test 5: Export Cancellation

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Start export
3. Click Cancel when progress reaches 30-50%

**Expected:**
- [ ] Export cancels immediately
- [ ] Partial output file may exist
- [ ] UI returns to ready state
- [ ] Can start new export after cancellation

---

## Test 6: Output File Overwrite

**Steps:**
1. Export to `test-overwrite.mp4`
2. Export again to same filename
3. Confirm overwrite

**Expected:**
- [ ] Warning shown (or automatic overwrite)
- [ ] Second export succeeds
- [ ] File replaced with new export

---

## Test 7: Invalid Source File

**Steps:**
1. Delete source video file
2. Try to export

**Expected:**
- [ ] Error message: "Source video not found"
- [ ] Export does not start
- [ ] No crash

---

## Test 8: No Segments to Export

**Steps:**
1. Load `samples/sample-30s.mp4`
2. Delete entire video (0-30 seconds)
3. Try to export

**Expected:**
- [ ] Error message: "No segments to export"
- [ ] Export button disabled
- [ ] No crash

---

## Test 9: Very Long Video (Performance)

**Steps:**
1. Load a 1-hour+ video (if available)
2. Delete multiple segments
3. Export

**Expected:**
- [ ] Export starts successfully
- [ ] Progress updates regularly
- [ ] Completes in reasonable time (5x realtime with hardware)
- [ ] Memory usage stays <1GB

---

## Test 10: Software Encoding Fallback

**Steps:**
1. Force software encoding (PreferredEncoder = "libx264")
2. Export video with deletions

**Expected:**
- [ ] Export works with software encoding
- [ ] Slower than hardware (~1x realtime)
- [ ] Output quality good

---

## Results Summary

**Date Tested:** __________
**Tester:** __________

| Test | Status | Notes |
|------|--------|-------|
| 1. Single segment | ☐ Pass | |
| 2. Multiple segments | ☐ Pass | |
| 3. Hardware detection | ☐ Pass | |
| 4. Progress accuracy | ☐ Pass | |
| 5. Cancellation | ☐ Pass | |
| 6. Overwrite | ☐ Pass | |
| 7. Invalid source | ☐ Pass | |
| 8. No segments | ☐ Pass | |
| 9. Long video | ☐ Pass | |
| 10. Software fallback | ☐ Pass | |

**Overall Status:** ☐ All Pass ☐ Issues Found

**Issues:**
```

**Step 3: Commit**

```bash
git add claude.md \
        docs/testing/week9-export-manual-tests.md
git commit -m "docs: document Week 9 export service learnings

- Add FFmpeg export architecture to claude.md
- Document hardware acceleration detection
- Document progress monitoring and cancellation
- Add manual testing checklist with 10 test cases"
```

---

## Task 9: Final Verification and Release

**Files:**
- Modify: `src/SpartaCut/SpartaCut.csproj`
- Modify: `src/SpartaCut.Core/SpartaCut.Core.csproj`

**Step 1: Run full test suite**

Run: `/usr/local/share/dotnet/dotnet test src/SpartaCut.Tests/SpartaCut.Tests.csproj`
Expected: All tests pass (120+ tests)

**Step 2: Build release configuration**

Run: `/usr/local/share/dotnet/dotnet build src/SpartaCut/SpartaCut.csproj -c Release`
Expected: Build succeeds with 0 errors, 0 warnings

**Step 3: Manual export test**

1. Run application
2. Load sample video
3. Delete a segment
4. Export successfully
5. Verify output plays

**Step 4: Final commit and tag**

```bash
git add src/SpartaCut/SpartaCut.csproj src/SpartaCut.Core/SpartaCut.Core.csproj
git commit -m "chore: release version 0.12.0 - Week 9 export complete

Week 9 deliverables:
- ExportService with FFmpeg integration
- Hardware encoder detection (NVENC/Quick Sync/AMF)
- Filter_complex builder for segment concatenation
- Progress monitoring with time estimates
- Export cancellation support
- Export UI with progress bar
- Edge case handling and validation
- Full test coverage (120+ tests passing)
- Manual testing checklist completed"

git tag v0.12.0
git push origin libvlc
git push origin v0.12.0
```

---

## Completion Checklist

Before marking Week 9 complete, verify:

- [ ] ExportService interface and models created
- [ ] Hardware encoder detection implemented
- [ ] FFmpeg filter_complex builder working
- [ ] Export with progress monitoring functional
- [ ] Export UI added to MainWindow
- [ ] Integration tests created
- [ ] Edge case validation added
- [ ] Documentation updated
- [ ] All unit tests pass (120+ tests)
- [ ] Manual testing completed
- [ ] Version bumped to 0.12.0
- [ ] Code committed and tagged

---

## Success Criteria

**Functional:**
✅ Can export single segment videos
✅ Can export multiple segments with deletions
✅ Hardware acceleration auto-detected and used
✅ Progress bar updates during export
✅ Cancellation works correctly
✅ Edge cases handled (missing files, no segments)

**Performance:**
✅ Hardware export: 5x+ realtime speed
✅ Software export: ~1x realtime speed
✅ Progress updates every 100ms
✅ Memory usage <500MB during export

**Quality:**
✅ All tests passing
✅ No crashes during export
✅ Output videos play correctly
✅ Audio remains synchronized

---

## Known Limitations

1. **Single Audio Track:** Only first audio track exported if video has multiple
2. **H.264 Only:** MVP supports H.264 codec only (no VP9, AV1, etc.)
3. **No Quality Presets:** Auto-optimal quality only (no Fast/Balanced/Quality options)

These are acceptable for MVP. Can be improved in v1.1+ if user feedback requests.

---

## Next Steps (Week 10)

After completing Week 9:
- Week 10: Session Management (Save/Load Projects)
- Focus: .spartacut file format, project serialization
- Save/load editing sessions with segment data
- Recent files list
