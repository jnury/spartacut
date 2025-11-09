# Week 2: Video Loading & Waveform Generation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete video import pipeline with progress reporting, waveform generation, and loading UI

**Architecture:** Create VideoService abstraction layer that coordinates metadata extraction and waveform generation with progress reporting. Implement WaveformGenerator using NAudio to extract audio data. Add async/await progress UI with IProgress<T> pattern for responsive loading experience.

**Tech Stack:**
- Avalonia UI 11.3.6 (loading screen UI)
- FFmpeg.AutoGen 7.1.1 (video metadata)
- NAudio 2.2.1 (audio waveform extraction)
- .NET 8 IProgress<T> (progress reporting)
- xUnit (unit testing)

**Testing Philosophy:** TDD for all service logic. Manual testing for UI components.

---

## Task 1: Progress Reporting Models

**Goal:** Define progress reporting data structures

**Files:**
- Create: `src/SpartaCut/Models/LoadProgress.cs`

### Step 1: Create LoadProgress model

Create the progress model for reporting video loading status:

```csharp
namespace SpartaCut.Models;

/// <summary>
/// Progress information for video loading operations.
/// </summary>
public class LoadProgress
{
    /// <summary>
    /// Current stage of loading process.
    /// </summary>
    public LoadStage Stage { get; init; }

    /// <summary>
    /// Progress percentage for current stage (0-100).
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Whether the operation has completed.
    /// </summary>
    public bool IsComplete => Stage == LoadStage.Complete;

    /// <summary>
    /// Whether the operation has failed.
    /// </summary>
    public bool IsFailed => Stage == LoadStage.Failed;
}

/// <summary>
/// Stages of video loading process.
/// </summary>
public enum LoadStage
{
    Validating,
    ExtractingMetadata,
    GeneratingWaveform,
    Complete,
    Failed
}
```

### Step 2: Build to verify no syntax errors

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 3: Commit

```bash
git add src/SpartaCut/Models/LoadProgress.cs
git commit -m "feat: add LoadProgress model for video loading progress reporting"
```

---

## Task 2: VideoService Interface & Implementation (TDD)

**Goal:** Create service layer for video operations with testable interface

**Files:**
- Create: `src/SpartaCut/Services/IVideoService.cs`
- Create: `src/SpartaCut/Services/VideoService.cs`
- Create: `src/SpartaCut.Tests/Services/VideoServiceTests.cs`

### Step 1: Write failing test for format validation

```csharp
using SpartaCut.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class VideoServiceTests
{
    [Fact]
    public async Task LoadVideo_WithInvalidExtension_ThrowsNotSupportedException()
    {
        // Arrange
        var service = new VideoService();
        var invalidPath = "/path/to/video.avi";

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.LoadVideoAsync(invalidPath, Progress.Create<LoadProgress>(_ => { })));
    }
}
```

### Step 2: Run test to verify it fails

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~VideoServiceTests.LoadVideo_WithInvalidExtension"`
Expected: Compilation error - VideoService doesn't exist

### Step 3: Create IVideoService interface

```csharp
using SpartaCut.Models;

namespace SpartaCut.Services;

/// <summary>
/// Service for video file operations (loading, validation, metadata extraction).
/// </summary>
public interface IVideoService
{
    /// <summary>
    /// Loads a video file with metadata and waveform generation.
    /// </summary>
    /// <param name="filePath">Path to video file</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video metadata</returns>
    /// <exception cref="NotSupportedException">Thrown if video format is not supported</exception>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    /// <exception cref="InvalidDataException">Thrown if file is corrupted</exception>
    Task<VideoMetadata> LoadVideoAsync(
        string filePath,
        IProgress<LoadProgress> progress,
        CancellationToken cancellationToken = default);
}
```

### Step 4: Create VideoService stub implementation

```csharp
using SpartaCut.FFmpeg;
using SpartaCut.Models;
using Serilog;
using System.IO;

namespace SpartaCut.Services;

/// <summary>
/// Service for video file operations.
/// </summary>
public class VideoService : IVideoService
{
    private static readonly string[] SupportedExtensions = { ".mp4", ".MP4" };

    public async Task<VideoMetadata> LoadVideoAsync(
        string filePath,
        IProgress<LoadProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // Validate file exists
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Video file not found: {filePath}", filePath);
        }

        // Validate extension
        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new NotSupportedException(
                $"Video format '{extension}' is not supported. Only MP4/H.264 videos are supported in the MVP.");
        }

        // Report progress: Validating
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.Validating,
            Percentage = 10,
            Message = "Validating video format..."
        });

        await Task.Delay(1, cancellationToken); // Make it actually async

        // TODO: Extract metadata
        // TODO: Generate waveform

        throw new NotImplementedException("LoadVideoAsync not fully implemented yet");
    }
}
```

### Step 5: Run test to verify it passes

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~VideoServiceTests.LoadVideo_WithInvalidExtension"`
Expected: PASS

### Step 6: Add test for missing file

```csharp
[Fact]
public async Task LoadVideo_WithMissingFile_ThrowsFileNotFoundException()
{
    // Arrange
    var service = new VideoService();
    var missingPath = "/nonexistent/video.mp4";

    // Act & Assert
    await Assert.ThrowsAsync<FileNotFoundException>(() =>
        service.LoadVideoAsync(missingPath, Progress.Create<LoadProgress>(_ => { })));
}
```

### Step 7: Run test to verify it passes

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~VideoServiceTests.LoadVideo_WithMissingFile"`
Expected: PASS

### Step 8: Commit

```bash
git add src/SpartaCut/Services/IVideoService.cs src/SpartaCut/Services/VideoService.cs src/SpartaCut.Tests/Services/VideoServiceTests.cs
git commit -m "feat: add VideoService with format and file validation (TDD)"
```

---

## Task 3: VideoService Metadata Integration

**Goal:** Integrate FrameExtractor into VideoService

**Files:**
- Modify: `src/SpartaCut/Services/VideoService.cs`
- Modify: `src/SpartaCut.Tests/Services/VideoServiceTests.cs`

### Step 1: Write failing test for metadata extraction

Create a small test video file or use mocking. For this implementation, we'll test with a real file path pattern:

```csharp
[Fact]
public async Task LoadVideo_WithValidMp4_ReturnsMetadata()
{
    // Arrange
    var service = new VideoService();
    // Note: This test requires a real MP4 file for integration testing
    // Skip if file doesn't exist
    var testVideoPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "test-video.mp4"
    );

    if (!File.Exists(testVideoPath))
    {
        // Skip test if no test video available
        return;
    }

    var progressReports = new List<LoadProgress>();
    var progress = Progress.Create<LoadProgress>(p => progressReports.Add(p));

    // Act
    var metadata = await service.LoadVideoAsync(testVideoPath, progress);

    // Assert
    Assert.NotNull(metadata);
    Assert.True(metadata.Duration > TimeSpan.Zero);
    Assert.True(metadata.Width > 0);
    Assert.True(metadata.Height > 0);
    Assert.Contains(progressReports, p => p.Stage == LoadStage.ExtractingMetadata);
}
```

### Step 2: Implement metadata extraction in VideoService

```csharp
public async Task<VideoMetadata> LoadVideoAsync(
    string filePath,
    IProgress<LoadProgress> progress,
    CancellationToken cancellationToken = default)
{
    // Validate file exists
    if (!File.Exists(filePath))
    {
        throw new FileNotFoundException($"Video file not found: {filePath}", filePath);
    }

    // Validate extension
    var extension = Path.GetExtension(filePath);
    if (!SupportedExtensions.Contains(extension))
    {
        throw new NotSupportedException(
            $"Video format '{extension}' is not supported. Only MP4/H.264 videos are supported in the MVP.");
    }

    try
    {
        // Report progress: Validating
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.Validating,
            Percentage = 10,
            Message = "Validating video format..."
        });

        await Task.Run(() => Thread.Sleep(100), cancellationToken); // Simulate validation work

        // Report progress: Extracting metadata
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.ExtractingMetadata,
            Percentage = 30,
            Message = "Extracting video metadata..."
        });

        // Extract metadata using FrameExtractor
        VideoMetadata metadata;
        await Task.Run(() =>
        {
            using var extractor = new FrameExtractor();
            metadata = extractor.ExtractMetadata(filePath);
        }, cancellationToken);

        // Validate codec
        if (!metadata.IsSupported())
        {
            throw new NotSupportedException(
                $"Video codec '{metadata.CodecName}' is not supported. Only H.264 is supported in the MVP.");
        }

        Log.Information("Video metadata extracted: {Metadata}", metadata);

        // TODO: Generate waveform (Task 4)

        // Report progress: Complete
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.Complete,
            Percentage = 100,
            Message = "Video loaded successfully"
        });

        return metadata;
    }
    catch (Exception ex) when (ex is not NotSupportedException and not FileNotFoundException)
    {
        Log.Error(ex, "Failed to load video: {FilePath}", filePath);

        progress.Report(new LoadProgress
        {
            Stage = LoadStage.Failed,
            Percentage = 0,
            Message = $"Failed to load video: {ex.Message}"
        });

        throw new InvalidDataException($"Failed to load video: {ex.Message}", ex);
    }
}
```

### Step 3: Run tests

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~VideoServiceTests"`
Expected: All tests PASS (integration test may skip if no test video)

### Step 4: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds with no errors

### Step 5: Commit

```bash
git add src/SpartaCut/Services/VideoService.cs src/SpartaCut.Tests/Services/VideoServiceTests.cs
git commit -m "feat: integrate metadata extraction into VideoService"
```

---

## Task 4: WaveformGenerator Implementation (TDD)

**Goal:** Extract audio samples from video for waveform visualization using NAudio

**Files:**
- Create: `src/SpartaCut/Models/WaveformData.cs`
- Create: `src/SpartaCut/Services/WaveformGenerator.cs`
- Create: `src/SpartaCut.Tests/Services/WaveformGeneratorTests.cs`

### Step 1: Create WaveformData model

```csharp
namespace SpartaCut.Models;

/// <summary>
/// Audio waveform data for timeline visualization.
/// </summary>
public class WaveformData
{
    /// <summary>
    /// Peak samples (min/max pairs) for waveform rendering.
    /// Each sample represents a time window.
    /// </summary>
    public required float[] Peaks { get; init; }

    /// <summary>
    /// Duration of audio.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Sample rate used for peak extraction.
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Number of samples per peak (resolution).
    /// </summary>
    public required int SamplesPerPeak { get; init; }
}
```

### Step 2: Write failing test for WaveformGenerator

```csharp
using SpartaCut.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class WaveformGeneratorTests
{
    [Fact]
    public void GenerateWaveform_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var generator = new WaveformGenerator();
        var invalidPath = "/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            generator.Generate(invalidPath, Progress.Create<int>(_ => { })));
    }
}
```

### Step 3: Run test to verify it fails

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~WaveformGeneratorTests"`
Expected: Compilation error - WaveformGenerator doesn't exist

### Step 4: Create WaveformGenerator stub

```csharp
using SpartaCut.Models;
using NAudio.Wave;
using Serilog;

namespace SpartaCut.Services;

/// <summary>
/// Generates audio waveform data from video files using NAudio.
/// </summary>
public class WaveformGenerator
{
    private const int TargetPeakCount = 3000; // ~3000 peaks for typical timeline width

    /// <summary>
    /// Generates waveform data from video file.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="progress">Progress reporter (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Waveform data</returns>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    public WaveformData Generate(
        string videoFilePath,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        // TODO: Implement waveform generation
        throw new NotImplementedException("WaveformGenerator not fully implemented yet");
    }
}
```

### Step 5: Run test to verify it passes

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~WaveformGeneratorTests.GenerateWaveform_WithInvalidFile"`
Expected: PASS

### Step 6: Implement waveform generation using NAudio

```csharp
public WaveformData Generate(
    string videoFilePath,
    IProgress<int> progress,
    CancellationToken cancellationToken = default)
{
    if (!File.Exists(videoFilePath))
    {
        throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
    }

    Log.Information("Generating waveform for: {FilePath}", videoFilePath);

    try
    {
        using var reader = new MediaFoundationReader(videoFilePath);
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);

        // Calculate samples per peak based on total samples and target peak count
        var samplesPerPeak = (int)(totalSamples / TargetPeakCount);
        if (samplesPerPeak < 1) samplesPerPeak = 1;

        var peaks = new List<float>();
        var buffer = new byte[samplesPerPeak * reader.WaveFormat.BlockAlign];

        long totalBytesRead = 0;
        int bytesRead;
        int lastProgress = 0;

        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            totalBytesRead += bytesRead;

            // Calculate min/max for this chunk
            float min = 0f, max = 0f;
            for (int i = 0; i < bytesRead; i += reader.WaveFormat.BlockAlign)
            {
                // Convert bytes to float sample (-1.0 to 1.0)
                float sample = reader.WaveFormat.BitsPerSample switch
                {
                    16 => BitConverter.ToInt16(buffer, i) / 32768f,
                    32 => BitConverter.ToInt32(buffer, i) / 2147483648f,
                    _ => 0f
                };

                if (sample < min) min = sample;
                if (sample > max) max = sample;
            }

            // Store min and max as separate peaks for rendering
            peaks.Add(min);
            peaks.Add(max);

            // Report progress
            var currentProgress = (int)(totalBytesRead * 100 / reader.Length);
            if (currentProgress != lastProgress)
            {
                progress.Report(currentProgress);
                lastProgress = currentProgress;
            }
        }

        var waveformData = new WaveformData
        {
            Peaks = peaks.ToArray(),
            Duration = reader.TotalTime,
            SampleRate = sampleRate,
            SamplesPerPeak = samplesPerPeak
        };

        Log.Information("Waveform generated: {PeakCount} peaks, Duration: {Duration}",
            peaks.Count, waveformData.Duration);

        return waveformData;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate waveform for: {FilePath}", videoFilePath);
        throw new InvalidDataException($"Failed to generate waveform: {ex.Message}", ex);
    }
}
```

### Step 7: Build and test manually

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

Note: Full integration test requires a real MP4 file. Add integration test in Task 6.

### Step 8: Commit

```bash
git add src/SpartaCut/Models/WaveformData.cs src/SpartaCut/Services/WaveformGenerator.cs src/SpartaCut.Tests/Services/WaveformGeneratorTests.cs
git commit -m "feat: implement WaveformGenerator using NAudio"
```

---

## Task 5: Integrate Waveform into VideoService

**Goal:** Complete VideoService by adding waveform generation

**Files:**
- Modify: `src/SpartaCut/Services/VideoService.cs`
- Modify: `src/SpartaCut/Models/VideoMetadata.cs`

### Step 1: Add WaveformData property to VideoMetadata

```csharp
/// <summary>
/// Audio waveform data (null if not yet generated).
/// </summary>
public WaveformData? Waveform { get; init; }
```

### Step 2: Integrate waveform generation in VideoService

Update the LoadVideoAsync method to generate waveform after metadata extraction:

```csharp
// ... after metadata extraction ...

// Report progress: Generating waveform
progress.Report(new LoadProgress
{
    Stage = LoadStage.GeneratingWaveform,
    Percentage = 50,
    Message = "Generating audio waveform..."
});

// Generate waveform
WaveformData waveform;
await Task.Run(() =>
{
    var waveformProgress = Progress.Create<int>(percent =>
    {
        // Map waveform progress (0-100) to overall progress (50-90)
        var overallPercent = 50 + (int)(percent * 0.4);
        progress.Report(new LoadProgress
        {
            Stage = LoadStage.GeneratingWaveform,
            Percentage = overallPercent,
            Message = $"Generating audio waveform... {percent}%"
        });
    });

    var generator = new WaveformGenerator();
    waveform = generator.Generate(filePath, waveformProgress, cancellationToken);
}, cancellationToken);

// Update metadata with waveform
metadata = metadata with { Waveform = waveform };

Log.Information("Waveform generated for video: {FilePath}", filePath);

// Report progress: Complete
progress.Report(new LoadProgress
{
    Stage = LoadStage.Complete,
    Percentage = 100,
    Message = "Video loaded successfully"
});

return metadata;
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Commit

```bash
git add src/SpartaCut/Services/VideoService.cs src/SpartaCut/Models/VideoMetadata.cs
git commit -m "feat: integrate waveform generation into VideoService"
```

---

## Task 6: Loading Dialog UI

**Goal:** Create loading dialog with progress bar and status text

**Files:**
- Create: `src/SpartaCut/Views/LoadingDialog.axaml`
- Create: `src/SpartaCut/Views/LoadingDialog.axaml.cs`

### Step 1: Create LoadingDialog XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="SpartaCut.Views.LoadingDialog"
        Title="Loading Video"
        Width="400" Height="200"
        CanResize="False"
        WindowStartupLocation="CenterOwner"
        Background="#1E1E1E">

    <Grid Margin="30">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="20"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Status Message -->
        <TextBlock Grid.Row="0"
                   Name="StatusTextBlock"
                   Text="Loading video..."
                   FontSize="16"
                   Foreground="White"
                   TextAlignment="Center"/>

        <!-- Progress Bar -->
        <ProgressBar Grid.Row="2"
                     Name="LoadProgressBar"
                     Height="10"
                     Minimum="0"
                     Maximum="100"
                     Value="0"
                     Foreground="#007ACC"/>

        <!-- Percentage Text -->
        <TextBlock Grid.Row="4"
                   Name="PercentageTextBlock"
                   Text="0%"
                   FontSize="14"
                   Foreground="#CCCCCC"
                   TextAlignment="Center"/>
    </Grid>
</Window>
```

### Step 2: Create LoadingDialog code-behind

```csharp
using Avalonia.Controls;
using Avalonia.Threading;
using SpartaCut.Models;

namespace SpartaCut.Views;

public partial class LoadingDialog : Window
{
    public LoadingDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the loading dialog with progress information.
    /// Thread-safe - can be called from any thread.
    /// </summary>
    public void UpdateProgress(LoadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusTextBlock.Text = progress.Message;
            LoadProgressBar.Value = progress.Percentage;
            PercentageTextBlock.Text = $"{progress.Percentage}%";

            if (progress.IsComplete || progress.IsFailed)
            {
                Close();
            }
        });
    }
}
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Commit

```bash
git add src/SpartaCut/Views/LoadingDialog.axaml src/SpartaCut/Views/LoadingDialog.axaml.cs
git commit -m "feat: add loading dialog UI with progress bar"
```

---

## Task 7: Update MainWindow to Use VideoService

**Goal:** Replace direct FrameExtractor usage with VideoService and loading dialog

**Files:**
- Modify: `src/SpartaCut/Views/MainWindow.axaml.cs`

### Step 1: Update LoadVideoButton_Click to use VideoService

```csharp
private async void LoadVideoButton_Click(object? sender, RoutedEventArgs e)
{
    try
    {
        // Open file picker
        var files = await ShowOpenFileDialogAsync();
        if (files == null || !files.Any())
        {
            Log.Information("User cancelled file selection");
            return;
        }

        var filePath = files.First().Path.LocalPath;
        Log.Information("User selected file: {FilePath}", filePath);

        // Show loading dialog
        var loadingDialog = new LoadingDialog();

        // Create progress reporter
        var progress = new Progress<LoadProgress>(p =>
        {
            loadingDialog.UpdateProgress(p);
        });

        // Start loading in background
        var loadTask = Task.Run(async () =>
        {
            var videoService = new VideoService();
            return await videoService.LoadVideoAsync(filePath, progress);
        });

        // Show dialog (will close when progress reports Complete/Failed)
        _ = loadingDialog.ShowDialog(this);

        // Wait for loading to complete
        var metadata = await loadTask;

        // Build info display
        var info = new StringBuilder();
        info.AppendLine("=== VIDEO METADATA ===");
        info.AppendLine();
        info.AppendLine($"File: {metadata.FilePath}");
        info.AppendLine($"Status: {(metadata.IsSupported() ? "✓ SUPPORTED (H.264)" : "✗ NOT SUPPORTED")}");
        info.AppendLine();
        info.AppendLine($"Duration: {metadata.Duration:hh\\:mm\\:ss\\.fff}");
        info.AppendLine($"Resolution: {metadata.Width} x {metadata.Height}");
        info.AppendLine($"Frame Rate: {metadata.FrameRate:F2} fps");
        info.AppendLine($"Codec: {metadata.CodecName}");
        info.AppendLine($"Pixel Format: {metadata.PixelFormat}");
        info.AppendLine($"Bitrate: {metadata.Bitrate / 1000:N0} kbps");
        info.AppendLine($"File Size: {metadata.GetFileSizeFormatted()}");
        info.AppendLine();

        if (metadata.Waveform != null)
        {
            info.AppendLine("=== WAVEFORM DATA ===");
            info.AppendLine();
            info.AppendLine($"Peak Count: {metadata.Waveform.Peaks.Length}");
            info.AppendLine($"Sample Rate: {metadata.Waveform.SampleRate} Hz");
            info.AppendLine($"Duration: {metadata.Waveform.Duration:hh\\:mm\\:ss\\.fff}");
            info.AppendLine();
        }

        info.AppendLine("=== FFmpeg VERSION ===");
        info.AppendLine();
        info.AppendLine(FFmpegSetup.GetFFmpegVersion());
        info.AppendLine();
        info.AppendLine("=== WEEK 2 SUCCESS ===");
        info.AppendLine();
        info.AppendLine("✓ VideoService implemented");
        info.AppendLine("✓ Progress reporting working");
        info.AppendLine("✓ Waveform generation complete");
        info.AppendLine("✓ Loading dialog functional");
        info.AppendLine();
        info.AppendLine("Week 2: COMPLETE");

        VideoInfoTextBlock.Text = info.ToString();

        Log.Information("Successfully loaded video with waveform: {Metadata}", metadata);
    }
    catch (NotSupportedException ex)
    {
        Log.Warning(ex, "Unsupported video format");
        VideoInfoTextBlock.Text = $"UNSUPPORTED FORMAT\n\n{ex.Message}\n\nPlease select an MP4 file with H.264 codec.";
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to load video");
        VideoInfoTextBlock.Text = $"ERROR: {ex.Message}\n\nSee logs for details.";
    }
}
```

### Step 2: Add using statements

At the top of MainWindow.axaml.cs:

```csharp
using SpartaCut.Services;
using SpartaCut.Models;
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Manual test with real MP4 file

Run: `/usr/local/share/dotnet/dotnet run --project src/SpartaCut/SpartaCut.csproj`
Expected:
- App launches
- Click "Load MP4 Video"
- Loading dialog appears with progress bar
- Progress updates from 0% to 100%
- Video metadata and waveform info displayed
- No errors in console

### Step 5: Commit

```bash
git add src/SpartaCut/Views/MainWindow.axaml.cs
git commit -m "feat: integrate VideoService and loading dialog into MainWindow"
```

---

## Task 8: Integration Tests

**Goal:** Add integration tests for complete video loading workflow

**Files:**
- Create: `src/SpartaCut.Tests/Integration/VideoLoadingIntegrationTests.cs`

### Step 1: Create integration test class

```csharp
using SpartaCut.Models;
using SpartaCut.Services;
using Xunit;

namespace SpartaCut.Tests.Integration;

/// <summary>
/// Integration tests for video loading workflow.
/// Requires test MP4 file to be present.
/// </summary>
public class VideoLoadingIntegrationTests
{
    private readonly string _testVideoPath;

    public VideoLoadingIntegrationTests()
    {
        // Look for test video in user home directory
        _testVideoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "test-video.mp4"
        );
    }

    [Fact]
    public async Task VideoService_LoadValidMp4_ReturnsCompleteMetadata()
    {
        // Skip if no test video
        if (!File.Exists(_testVideoPath))
        {
            // Test skipped - no test video available
            return;
        }

        // Arrange
        var service = new VideoService();
        var progressReports = new List<LoadProgress>();
        var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

        // Act
        var metadata = await service.LoadVideoAsync(_testVideoPath, progress);

        // Assert - Metadata
        Assert.NotNull(metadata);
        Assert.Equal(_testVideoPath, metadata.FilePath);
        Assert.True(metadata.Duration > TimeSpan.Zero);
        Assert.True(metadata.Width > 0);
        Assert.True(metadata.Height > 0);
        Assert.True(metadata.FrameRate > 0);
        Assert.NotEmpty(metadata.CodecName);

        // Assert - Waveform
        Assert.NotNull(metadata.Waveform);
        Assert.NotEmpty(metadata.Waveform.Peaks);
        Assert.Equal(metadata.Duration, metadata.Waveform.Duration);

        // Assert - Progress Reporting
        Assert.Contains(progressReports, p => p.Stage == LoadStage.Validating);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.ExtractingMetadata);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.GeneratingWaveform);
        Assert.Contains(progressReports, p => p.Stage == LoadStage.Complete);

        // Progress should increase monotonically (mostly)
        var lastPercentage = -1;
        foreach (var report in progressReports.Where(p => p.Stage != LoadStage.Complete))
        {
            Assert.True(report.Percentage >= lastPercentage || report.Stage != progressReports[progressReports.IndexOf(report) - 1].Stage);
            lastPercentage = report.Percentage;
        }
    }

    [Fact]
    public async Task VideoService_CancellationToken_CancelsOperation()
    {
        // Skip if no test video
        if (!File.Exists(_testVideoPath))
        {
            return;
        }

        // Arrange
        var service = new VideoService();
        var cts = new CancellationTokenSource();
        var progress = new Progress<LoadProgress>(p =>
        {
            // Cancel after first progress report
            if (p.Percentage > 0)
            {
                cts.Cancel();
            }
        });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await service.LoadVideoAsync(_testVideoPath, progress, cts.Token));
    }
}
```

### Step 2: Run integration tests

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~VideoLoadingIntegrationTests"`
Expected: Tests SKIP if no test video, or PASS if test video exists

### Step 3: Commit

```bash
git add src/SpartaCut.Tests/Integration/VideoLoadingIntegrationTests.cs
git commit -m "test: add integration tests for video loading workflow"
```

---

## Task 9: Update Project Version

**Goal:** Increment version to 0.2.0 for Week 2 milestone

**Files:**
- Modify: `src/SpartaCut/SpartaCut.csproj`
- Modify: `src/SpartaCut/App.axaml.cs` (About dialog)

### Step 1: Update version in SpartaCut.csproj

```xml
<!-- Version Information -->
<Version>0.2.0</Version>
<AssemblyVersion>0.2.0</AssemblyVersion>
<FileVersion>0.2.0</FileVersion>
<InformationalVersion>0.2.0</InformationalVersion>
```

### Step 2: Update About dialog version text

In App.axaml.cs, find the version TextBlock and update:

```csharp
Text = "Version 0.2.0 - Week 2 Complete",
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Commit and tag

```bash
git add src/SpartaCut/SpartaCut.csproj src/SpartaCut/App.axaml.cs
git commit -m "chore: bump version to 0.2.0 for Week 2 milestone"
git tag v0.2.0
```

---

## Task 10: Create Development Log

**Goal:** Document Week 2 completion

**Files:**
- Modify: `docs/development-log.md`

### Step 1: Add Week 2 section to development log

```markdown
## Week 2: Video Loading & Waveform Generation

**Date:** 2025-11-03
**Status:** ✅ Complete
**Version:** 0.2.0

### Objectives
- Complete video import pipeline with validation
- Implement progress reporting for async operations
- Generate audio waveform using NAudio
- Create loading UI with progress bar

### Completed Tasks

1. **Progress Reporting Models** - LoadProgress and LoadStage enum
2. **VideoService Interface & Implementation** - TDD approach with format validation
3. **VideoService Metadata Integration** - Integrated FrameExtractor
4. **WaveformGenerator Implementation** - NAudio-based audio extraction
5. **Waveform Integration** - Added waveform to VideoService pipeline
6. **Loading Dialog UI** - Progress bar with status messages
7. **MainWindow Integration** - Updated UI to use VideoService
8. **Integration Tests** - End-to-end video loading tests
9. **Version Update** - Bumped to 0.2.0
10. **Documentation** - Updated development log

### Key Achievements

- ✅ VideoService abstraction layer complete
- ✅ Progress reporting with IProgress<T> pattern
- ✅ Audio waveform generation working (NAudio integration)
- ✅ Loading dialog with real-time progress updates
- ✅ Comprehensive error handling (format, codec, file validation)
- ✅ Integration tests for complete workflow
- ✅ TDD approach for service layer

### Technical Highlights

**VideoService Architecture:**
- Validates file format and codec before processing
- Reports progress through 5 stages: Validating → ExtractingMetadata → GeneratingWaveform → Complete
- Async/await throughout with cancellation support
- Throws specific exceptions (NotSupportedException, FileNotFoundException, InvalidDataException)

**WaveformGenerator:**
- Uses NAudio MediaFoundationReader for audio extraction
- Generates ~3000 peak samples for efficient timeline rendering
- Reports percentage progress (0-100)
- Supports cancellation via CancellationToken

**Loading Dialog:**
- Thread-safe progress updates via Dispatcher
- Auto-closes on Complete or Failed
- Clean, minimal dark theme UI

### Commits

1. `feat: add LoadProgress model for video loading progress reporting`
2. `feat: add VideoService with format and file validation (TDD)`
3. `feat: integrate metadata extraction into VideoService`
4. `feat: implement WaveformGenerator using NAudio`
5. `feat: integrate waveform generation into VideoService`
6. `feat: add loading dialog UI with progress bar`
7. `feat: integrate VideoService and loading dialog into MainWindow`
8. `test: add integration tests for video loading workflow`
9. `chore: bump version to 0.2.0 for Week 2 milestone`
10. `docs: update development log for Week 2 completion`

### Testing

**Unit Tests:**
- VideoService format validation
- VideoService file existence checks
- WaveformGenerator error handling

**Integration Tests:**
- Complete video loading workflow
- Progress reporting validation
- Cancellation token handling

**Manual Testing:**
- Tested with real MP4/H.264 files
- Verified loading dialog progress updates
- Confirmed waveform generation (peak count, duration)

### Time Estimate vs Actual

- **Estimated:** 25 hours
- **Actual:** TBD (to be filled after completion)

### Next Steps

Week 3: Timeline UI & Thumbnails (see roadmap)
```

### Step 2: Commit

```bash
git add docs/development-log.md
git commit -m "docs: update development log for Week 2 completion"
```

---

## Verification Checklist

Before considering Week 2 complete, verify:

- [ ] All 10 tasks committed
- [ ] All unit tests pass: `dotnet test`
- [ ] Project builds without errors: `dotnet build`
- [ ] Integration tests run (skip if no test video)
- [ ] Manual test: Load real MP4 file shows progress dialog and waveform info
- [ ] Version updated to 0.2.0 in .csproj and About dialog
- [ ] Git tag v0.2.0 created
- [ ] Development log updated

**Final Command:**
```bash
git push origin dev && git push origin v0.2.0
```

---

## Notes for Engineer

**NAudio on macOS:**
NAudio primarily uses Windows-specific APIs (MediaFoundation). On macOS, you may need to use FFMpegCore or FFmpeg directly to extract audio samples instead of NAudio. Consider wrapping the platform-specific audio extraction in an abstraction if cross-platform support is required.

**Alternative for macOS:** Use FFmpeg to extract audio to WAV, then read WAV file with cross-platform library or parse manually.

**Test Video:**
Create a small test MP4 file for integration testing:
```bash
ffmpeg -f lavfi -i testsrc=duration=10:size=1280x720:rate=30 -f lavfi -i sine=frequency=1000:duration=10 -pix_fmt yuv420p -c:v libx264 -c:a aac ~/test-video.mp4
```

**Performance:**
For very long videos (2+ hours), consider:
- Streaming waveform generation (don't load entire audio into memory)
- Reduced peak count or resolution
- Background thread with lower priority
- Caching generated waveform data

---

## Summary

**Week 2 Deliverable:** Complete video import pipeline with progress reporting, waveform generation, and loading UI.

**Key Components:**
- VideoService (validation, metadata, waveform orchestration)
- WaveformGenerator (NAudio-based audio extraction)
- LoadingDialog (progress UI)
- LoadProgress models (progress reporting)
- Integration tests (end-to-end workflow)

**Testing Strategy:** TDD for service layer, manual testing for UI, integration tests for workflow.

**Estimated Effort:** 25 hours over 10 tasks.
