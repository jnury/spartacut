# Week 3: Timeline UI & Thumbnails Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create custom timeline control displaying waveform visualization, video thumbnails, time ruler, and playhead with click-to-seek functionality

**Architecture:** Implement ThumbnailGenerator service to extract video frames at 5-second intervals. Create TimelineControl as custom Avalonia control using SkiaSharp for rendering waveform, thumbnails, time ruler, and playhead. Use MVVM pattern with TimelineViewModel to manage timeline state (current time, zoom level, dimensions). Implement click/drag handlers for playhead seeking.

**Tech Stack:**
- Avalonia UI 11.3.6 (custom control framework)
- SkiaSharp 2.88.x (2D graphics rendering)
- FFmpeg.AutoGen 7.1.1 (thumbnail extraction)
- .NET 8 (async/await, records)
- xUnit (unit testing)

**Testing Philosophy:** TDD for service logic (ThumbnailGenerator). Manual/visual testing for UI rendering (TimelineControl). Unit tests for ViewModel logic.

---

## Task 1: Thumbnail Data Models

**Goal:** Define data structures for thumbnail management

**Files:**
- Create: `src/Bref/Models/ThumbnailData.cs`
- Create: `src/Bref/Models/TimelineMetrics.cs`

### Step 1: Create ThumbnailData model

```csharp
namespace SpartaCut.Models;

/// <summary>
/// Represents a single video thumbnail at a specific time position.
/// </summary>
public record ThumbnailData
{
    /// <summary>
    /// Time position in the video where this thumbnail was extracted.
    /// </summary>
    public required TimeSpan TimePosition { get; init; }

    /// <summary>
    /// Thumbnail image data as byte array (JPEG format).
    /// </summary>
    public required byte[] ImageData { get; init; }

    /// <summary>
    /// Width of the thumbnail image in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Height of the thumbnail image in pixels.
    /// </summary>
    public required int Height { get; init; }
}
```

### Step 2: Create TimelineMetrics model

```csharp
namespace SpartaCut.Models;

/// <summary>
/// Metrics and dimensions for timeline rendering calculations.
/// </summary>
public record TimelineMetrics
{
    /// <summary>
    /// Total duration of the video.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Width of the timeline in pixels.
    /// </summary>
    public required double TimelineWidth { get; init; }

    /// <summary>
    /// Height of the timeline in pixels.
    /// </summary>
    public required double TimelineHeight { get; init; }

    /// <summary>
    /// Pixels per second ratio for timeline scaling.
    /// </summary>
    public double PixelsPerSecond => TimelineWidth / TotalDuration.TotalSeconds;

    /// <summary>
    /// Converts time position to pixel position on timeline.
    /// </summary>
    public double TimeToPixel(TimeSpan time) => time.TotalSeconds * PixelsPerSecond;

    /// <summary>
    /// Converts pixel position to time position.
    /// </summary>
    public TimeSpan PixelToTime(double pixel) => TimeSpan.FromSeconds(pixel / PixelsPerSecond);
}
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Commit

```bash
git add src/Bref/Models/ThumbnailData.cs src/Bref/Models/TimelineMetrics.cs
git commit -m "feat: add ThumbnailData and TimelineMetrics models"
```

---

## Task 2: ThumbnailGenerator Service (TDD)

**Goal:** Extract video thumbnails at regular intervals using FFmpeg

**Files:**
- Create: `src/Bref/Services/ThumbnailGenerator.cs`
- Create: `src/SpartaCut.Tests/Services/ThumbnailGeneratorTests.cs`

### Step 1: Write failing test for invalid file

```csharp
using SpartaCut.Services;
using Xunit;

namespace SpartaCut.Tests.Services;

public class ThumbnailGeneratorTests
{
    [Fact]
    public void Generate_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var generator = new ThumbnailGenerator();
        var invalidPath = "/nonexistent/video.mp4";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            generator.Generate(invalidPath, TimeSpan.FromSeconds(5), 160, 90));
    }
}
```

### Step 2: Run test to verify it fails

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~ThumbnailGeneratorTests.Generate_WithInvalidFile"`
Expected: Compilation error - ThumbnailGenerator doesn't exist

### Step 3: Create ThumbnailGenerator stub

```csharp
using SpartaCut.FFmpeg;
using SpartaCut.Models;
using FFmpeg.AutoGen;
using Serilog;

namespace SpartaCut.Services;

/// <summary>
/// Generates video thumbnails at regular intervals using FFmpeg.
/// </summary>
public unsafe class ThumbnailGenerator
{
    /// <summary>
    /// Generates thumbnails from video at specified intervals.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="interval">Time interval between thumbnails</param>
    /// <param name="width">Thumbnail width in pixels</param>
    /// <param name="height">Thumbnail height in pixels</param>
    /// <returns>List of thumbnail data</returns>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    public List<ThumbnailData> Generate(
        string videoFilePath,
        TimeSpan interval,
        int width,
        int height)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        // TODO: Implement thumbnail generation
        throw new NotImplementedException("ThumbnailGenerator not fully implemented yet");
    }
}
```

### Step 4: Run test to verify it passes

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~ThumbnailGeneratorTests.Generate_WithInvalidFile"`
Expected: PASS

### Step 5: Implement thumbnail generation

```csharp
public List<ThumbnailData> Generate(
    string videoFilePath,
    TimeSpan interval,
    int width,
    int height)
{
    if (!File.Exists(videoFilePath))
    {
        throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
    }

    Log.Information("Generating thumbnails for: {FilePath}, Interval: {Interval}s, Size: {Width}x{Height}",
        videoFilePath, interval.TotalSeconds, width, height);

    var thumbnails = new List<ThumbnailData>();

    try
    {
        // Initialize FFmpeg
        FFmpegSetup.Initialize();

        AVFormatContext* formatContext = null;
        if (ffmpeg.avformat_open_input(&formatContext, videoFilePath, null, null) != 0)
        {
            throw new InvalidDataException("Could not open video file");
        }

        try
        {
            if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
            {
                throw new InvalidDataException("Could not find stream information");
            }

            // Find video stream
            int videoStreamIndex = -1;
            AVCodecContext* codecContext = null;

            for (int i = 0; i < formatContext->nb_streams; i++)
            {
                if (formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStreamIndex = i;
                    break;
                }
            }

            if (videoStreamIndex == -1)
            {
                throw new InvalidDataException("No video stream found");
            }

            var codecParams = formatContext->streams[videoStreamIndex]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
            if (codec == null)
            {
                throw new InvalidDataException("Codec not found");
            }

            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (codecContext == null)
            {
                throw new OutOfMemoryException("Could not allocate codec context");
            }

            try
            {
                ffmpeg.avcodec_parameters_to_context(codecContext, codecParams);

                if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                {
                    throw new InvalidDataException("Could not open codec");
                }

                // Calculate total duration
                var stream = formatContext->streams[videoStreamIndex];
                var duration = TimeSpan.FromSeconds(stream->duration * ffmpeg.av_q2d(stream->time_base));

                // Generate thumbnails at intervals
                var currentTime = TimeSpan.Zero;
                while (currentTime < duration)
                {
                    var thumbnail = ExtractFrameAtTime(formatContext, codecContext, videoStreamIndex, currentTime, width, height);
                    if (thumbnail != null)
                    {
                        thumbnails.Add(thumbnail);
                    }

                    currentTime += interval;
                }

                Log.Information("Generated {Count} thumbnails for {FilePath}", thumbnails.Count, videoFilePath);
            }
            finally
            {
                if (codecContext != null)
                {
                    var ctx = codecContext;
                    ffmpeg.avcodec_free_context(&ctx);
                }
            }
        }
        finally
        {
            var ctx = formatContext;
            ffmpeg.avformat_close_input(&ctx);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to generate thumbnails for: {FilePath}", videoFilePath);
        throw new InvalidDataException($"Failed to generate thumbnails: {ex.Message}", ex);
    }

    return thumbnails;
}

private ThumbnailData? ExtractFrameAtTime(
    AVFormatContext* formatContext,
    AVCodecContext* codecContext,
    int videoStreamIndex,
    TimeSpan targetTime,
    int width,
    int height)
{
    try
    {
        // Seek to target time
        var stream = formatContext->streams[videoStreamIndex];
        long timestamp = (long)(targetTime.TotalSeconds / ffmpeg.av_q2d(stream->time_base));

        if (ffmpeg.av_seek_frame(formatContext, videoStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
        {
            Log.Warning("Failed to seek to {Time} in video", targetTime);
            return null;
        }

        ffmpeg.avcodec_flush_buffers(codecContext);

        var packet = ffmpeg.av_packet_alloc();
        var frame = ffmpeg.av_frame_alloc();
        var scaledFrame = ffmpeg.av_frame_alloc();

        try
        {
            // Read frames until we find one at or after target time
            while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
            {
                if (packet->stream_index == videoStreamIndex)
                {
                    if (ffmpeg.avcodec_send_packet(codecContext, packet) == 0)
                    {
                        if (ffmpeg.avcodec_receive_frame(codecContext, frame) == 0)
                        {
                            // Scale frame to thumbnail size
                            scaledFrame->width = width;
                            scaledFrame->height = height;
                            scaledFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;

                            ffmpeg.av_frame_get_buffer(scaledFrame, 32);

                            var swsContext = ffmpeg.sws_getContext(
                                codecContext->width, codecContext->height, codecContext->pix_fmt,
                                width, height, AVPixelFormat.AV_PIX_FMT_RGB24,
                                ffmpeg.SWS_BILINEAR, null, null, null);

                            if (swsContext == null)
                            {
                                throw new InvalidOperationException("Could not create scaling context");
                            }

                            try
                            {
                                ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, codecContext->height,
                                    scaledFrame->data, scaledFrame->linesize);

                                // Convert RGB24 frame to JPEG byte array
                                var imageData = FrameToJpeg(scaledFrame, width, height);

                                return new ThumbnailData
                                {
                                    TimePosition = targetTime,
                                    ImageData = imageData,
                                    Width = width,
                                    Height = height
                                };
                            }
                            finally
                            {
                                ffmpeg.sws_freeContext(swsContext);
                            }
                        }
                    }
                }

                ffmpeg.av_packet_unref(packet);
            }
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_frame_free(&scaledFrame);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to extract frame at {Time}", targetTime);
    }

    return null;
}

private byte[] FrameToJpeg(AVFrame* frame, int width, int height)
{
    // Simple JPEG encoding using SkiaSharp
    using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgb888x, SkiaSharp.SKAlphaType.Opaque);

    var ptr = bitmap.GetPixels();
    var dataPtr = (byte*)frame->data[0];
    var linesize = frame->linesize[0];

    for (int y = 0; y < height; y++)
    {
        Buffer.MemoryCopy(dataPtr + (y * linesize), (byte*)ptr + (y * width * 3), width * 3, width * 3);
    }

    using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85);

    return data.ToArray();
}
```

### Step 6: Add SkiaSharp package

Modify `src/Bref/SpartaCut.csproj`:
```xml
<PackageReference Include="SkiaSharp" Version="2.88.8" />
```

### Step 7: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 8: Commit

```bash
git add src/Bref/Services/ThumbnailGenerator.cs src/SpartaCut.Tests/Services/ThumbnailGeneratorTests.cs src/Bref/SpartaCut.csproj
git commit -m "feat: implement ThumbnailGenerator using FFmpeg and SkiaSharp"
```

---

## Task 3: TimelineViewModel

**Goal:** ViewModel to manage timeline state and interactions

**Files:**
- Create: `src/Bref/ViewModels/TimelineViewModel.cs`

### Step 1: Create TimelineViewModel

```csharp
using SpartaCut.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace SpartaCut.ViewModels;

/// <summary>
/// ViewModel for timeline control managing playhead position and timeline state.
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    [ObservableProperty]
    private VideoMetadata? _videoMetadata;

    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private double _timelineWidth = 1000;

    [ObservableProperty]
    private double _timelineHeight = 200;

    [ObservableProperty]
    private ObservableCollection<ThumbnailData> _thumbnails = new();

    /// <summary>
    /// Timeline metrics for coordinate conversions.
    /// </summary>
    public TimelineMetrics? Metrics =>
        VideoMetadata != null
            ? new TimelineMetrics
            {
                TotalDuration = VideoMetadata.Duration,
                TimelineWidth = TimelineWidth,
                TimelineHeight = TimelineHeight
            }
            : null;

    /// <summary>
    /// Playhead position in pixels.
    /// </summary>
    public double PlayheadPosition => Metrics?.TimeToPixel(CurrentTime) ?? 0;

    /// <summary>
    /// Seeks to a specific pixel position on the timeline.
    /// </summary>
    [RelayCommand]
    public void SeekToPixel(double pixelPosition)
    {
        if (Metrics == null) return;

        var newTime = Metrics.PixelToTime(pixelPosition);

        // Clamp to valid range
        if (newTime < TimeSpan.Zero)
            newTime = TimeSpan.Zero;
        else if (newTime > VideoMetadata!.Duration)
            newTime = VideoMetadata.Duration;

        CurrentTime = newTime;
        OnPropertyChanged(nameof(PlayheadPosition));
    }

    /// <summary>
    /// Loads video metadata and thumbnails.
    /// </summary>
    public void LoadVideo(VideoMetadata metadata, List<ThumbnailData> thumbnails)
    {
        VideoMetadata = metadata;
        Thumbnails = new ObservableCollection<ThumbnailData>(thumbnails);
        CurrentTime = TimeSpan.Zero;
        OnPropertyChanged(nameof(Metrics));
        OnPropertyChanged(nameof(PlayheadPosition));
    }
}
```

### Step 2: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 3: Commit

```bash
git add src/Bref/ViewModels/TimelineViewModel.cs
git commit -m "feat: add TimelineViewModel for timeline state management"
```

---

## Task 4: TimelineControl XAML

**Goal:** Create custom Avalonia control for timeline rendering

**Files:**
- Create: `src/Bref/Controls/TimelineControl.axaml`
- Create: `src/Bref/Controls/TimelineControl.axaml.cs`

### Step 1: Create TimelineControl XAML

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="200"
             x:Class="Bref.Controls.TimelineControl"
             Background="#2D2D2D">

    <Grid>
        <!-- Canvas for SkiaSharp rendering -->
        <Panel Name="RenderPanel"
               PointerPressed="OnPointerPressed"
               PointerMoved="OnPointerMoved"
               PointerReleased="OnPointerReleased">

            <!-- Placeholder for visual feedback during development -->
            <TextBlock Text="Timeline Control"
                       Foreground="White"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       FontSize="16"/>
        </Panel>
    </Grid>
</UserControl>
```

### Step 2: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 3: Commit

```bash
git add src/Bref/Controls/TimelineControl.axaml
git commit -m "feat: add TimelineControl XAML structure"
```

---

## Task 5: TimelineControl Rendering (SkiaSharp)

**Goal:** Implement custom rendering with SkiaSharp for waveform, thumbnails, ruler, and playhead

**Files:**
- Modify: `src/Bref/Controls/TimelineControl.axaml.cs`

### Step 1: Create TimelineControl code-behind stub

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SpartaCut.Models;
using SpartaCut.ViewModels;
using SkiaSharp;

namespace SpartaCut.Controls;

public partial class TimelineControl : UserControl
{
    private TimelineViewModel? _viewModel;
    private bool _isDragging;

    public TimelineControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as TimelineViewModel;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            if (_viewModel != null && Bounds.Width > 0)
            {
                _viewModel.TimelineWidth = Bounds.Width;
                _viewModel.TimelineHeight = Bounds.Height;
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel?.VideoMetadata == null) return;

        var point = e.GetPosition(this);
        _isDragging = true;
        _viewModel.SeekToPixel(point.X);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _viewModel?.VideoMetadata == null) return;

        var point = e.GetPosition(this);
        _viewModel.SeekToPixel(point.X);
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_viewModel?.VideoMetadata == null || _viewModel.Metrics == null)
            return;

        var renderTarget = context.PlatformImpl;
        if (renderTarget is ISkiaSharpApiLeaseFeature skiaFeature)
        {
            using var lease = skiaFeature.Lease();
            var canvas = lease?.SkCanvas;
            if (canvas != null)
            {
                RenderTimeline(canvas, _viewModel, Bounds);
            }
        }
    }

    private void RenderTimeline(SKCanvas canvas, TimelineViewModel viewModel, Rect bounds)
    {
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;

        // Clear background
        canvas.Clear(SKColor.Parse("#2D2D2D"));

        // Define regions
        var waveformHeight = height * 0.3f;
        var thumbnailHeight = height * 0.5f;
        var rulerHeight = height * 0.2f;

        // Render waveform
        RenderWaveform(canvas, viewModel, 0, waveformHeight, width);

        // Render thumbnails
        RenderThumbnails(canvas, viewModel, waveformHeight, thumbnailHeight, width);

        // Render time ruler
        RenderTimeRuler(canvas, viewModel, waveformHeight + thumbnailHeight, rulerHeight, width);

        // Render playhead
        RenderPlayhead(canvas, viewModel, height);
    }

    private void RenderWaveform(SKCanvas canvas, TimelineViewModel viewModel, float y, float height, float width)
    {
        if (viewModel.VideoMetadata?.Waveform?.Peaks == null) return;

        var waveform = viewModel.VideoMetadata.Waveform;
        var peaks = waveform.Peaks;
        var paint = new SKPaint
        {
            Color = SKColor.Parse("#007ACC"),
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Draw waveform peaks
        var centerY = y + height / 2;
        var samplesPerPixel = Math.Max(1, peaks.Length / (int)width / 2); // Peaks are min/max pairs

        for (int x = 0; x < width; x++)
        {
            var sampleIndex = (int)(x * samplesPerPixel * 2);
            if (sampleIndex + 1 >= peaks.Length) break;

            var min = peaks[sampleIndex];
            var max = peaks[sampleIndex + 1];

            var minY = centerY - (min * height / 2);
            var maxY = centerY - (max * height / 2);

            canvas.DrawLine(x, (float)minY, x, (float)maxY, paint);
        }
    }

    private void RenderThumbnails(SKCanvas canvas, TimelineViewModel viewModel, float y, float height, float width)
    {
        if (!viewModel.Thumbnails.Any()) return;

        var metrics = viewModel.Metrics!;
        var thumbnailPaint = new SKPaint { IsAntialias = true };

        foreach (var thumbnail in viewModel.Thumbnails)
        {
            var x = (float)metrics.TimeToPixel(thumbnail.TimePosition);

            // Load thumbnail image
            using var stream = new MemoryStream(thumbnail.ImageData);
            using var bitmap = SKBitmap.Decode(stream);

            if (bitmap != null)
            {
                var aspectRatio = (float)bitmap.Width / bitmap.Height;
                var thumbWidth = height * aspectRatio;
                var destRect = new SKRect(x, y, x + thumbWidth, y + height);

                canvas.DrawBitmap(bitmap, destRect, thumbnailPaint);

                // Draw border
                var borderPaint = new SKPaint
                {
                    Color = SKColor.Parse("#555555"),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    IsAntialias = true
                };
                canvas.DrawRect(destRect, borderPaint);
            }
        }
    }

    private void RenderTimeRuler(SKCanvas canvas, TimelineViewModel viewModel, float y, float height, float width)
    {
        var metrics = viewModel.Metrics!;
        var totalSeconds = (int)metrics.TotalDuration.TotalSeconds;

        // Draw ruler background
        var bgPaint = new SKPaint { Color = SKColor.Parse("#1E1E1E") };
        canvas.DrawRect(0, y, width, height, bgPaint);

        // Draw time markers
        var textPaint = new SKPaint
        {
            Color = SKColor.Parse("#CCCCCC"),
            TextSize = 12,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        var tickPaint = new SKPaint
        {
            Color = SKColor.Parse("#555555"),
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Determine marker interval (every 5, 10, 30, or 60 seconds based on zoom)
        var pixelsPerSecond = width / totalSeconds;
        var interval = pixelsPerSecond < 2 ? 60 : pixelsPerSecond < 5 ? 30 : pixelsPerSecond < 10 ? 10 : 5;

        for (int seconds = 0; seconds <= totalSeconds; seconds += interval)
        {
            var x = (float)metrics.TimeToPixel(TimeSpan.FromSeconds(seconds));

            // Draw tick
            canvas.DrawLine(x, y, x, y + height / 2, tickPaint);

            // Draw time label
            var timeStr = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            canvas.DrawText(timeStr, x, y + height - 5, textPaint);
        }
    }

    private void RenderPlayhead(SKCanvas canvas, TimelineViewModel viewModel, float height)
    {
        var x = (float)viewModel.PlayheadPosition;

        // Draw playhead line
        var linePaint = new SKPaint
        {
            Color = SKColor.Parse("#FF0000"),
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawLine(x, 0, x, height, linePaint);

        // Draw playhead handle (triangle at top)
        var handlePaint = new SKPaint
        {
            Color = SKColor.Parse("#FF0000"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var handlePath = new SKPath();
        handlePath.MoveTo(x, 0);
        handlePath.LineTo(x - 8, 15);
        handlePath.LineTo(x + 8, 15);
        handlePath.Close();
        canvas.DrawPath(handlePath, handlePaint);
    }
}
```

### Step 2: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 3: Commit

```bash
git add src/Bref/Controls/TimelineControl.axaml.cs
git commit -m "feat: implement TimelineControl rendering with SkiaSharp"
```

---

## Task 6: Integrate TimelineControl into MainWindow

**Goal:** Add TimelineControl to MainWindow and populate with video data

**Files:**
- Modify: `src/Bref/Views/MainWindow.axaml`
- Modify: `src/Bref/Views/MainWindow.axaml.cs`

### Step 1: Update MainWindow.axaml to include TimelineControl

```xml
<!-- Add namespace reference at top -->
xmlns:controls="using:Bref.Controls"
xmlns:vm="using:Bref.ViewModels"

<!-- Add after VideoInfoTextBlock -->
<controls:TimelineControl Grid.Row="3"
                          Name="TimelineControl"
                          Height="200"
                          Margin="0,20,0,0"
                          IsVisible="False"/>
```

Update Grid row definitions:
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="Auto"/> <!-- New row for timeline -->
</Grid.RowDefinitions>
```

### Step 2: Update MainWindow.axaml.cs to populate timeline

After successful video load (in LoadVideoButton_Click):

```csharp
// After displaying video info, generate thumbnails and show timeline
try
{
    VideoInfoTextBlock.Text += "\n\nGenerating thumbnails for timeline...";

    var thumbnailGenerator = new ThumbnailGenerator();
    var thumbnails = await Task.Run(() =>
        thumbnailGenerator.Generate(filePath, TimeSpan.FromSeconds(5), 160, 90));

    // Create timeline viewmodel
    var timelineViewModel = new TimelineViewModel();
    timelineViewModel.LoadVideo(metadata, thumbnails);

    // Set timeline datacontext and show
    TimelineControl.DataContext = timelineViewModel;
    TimelineControl.IsVisible = true;

    VideoInfoTextBlock.Text = VideoInfoTextBlock.Text.Replace(
        "\n\nGenerating thumbnails for timeline...",
        $"\n\nTimeline ready with {thumbnails.Count} thumbnails");

    Log.Information("Timeline populated with {Count} thumbnails", thumbnails.Count);
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to generate thumbnails for timeline");
    VideoInfoTextBlock.Text += $"\n\nWarning: Could not generate timeline thumbnails: {ex.Message}";
}
```

Add using statements:
```csharp
using SpartaCut.Controls;
using SpartaCut.ViewModels;
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Manual test

Run: `/usr/local/share/dotnet/dotnet run --project src/Bref/SpartaCut.csproj`
Expected:
- Load an MP4 video
- Timeline should appear below video info
- Should show waveform, thumbnails, time ruler, and red playhead
- Clicking timeline should move playhead

### Step 5: Commit

```bash
git add src/Bref/Views/MainWindow.axaml src/Bref/Views/MainWindow.axaml.cs
git commit -m "feat: integrate TimelineControl into MainWindow"
```

---

## Task 7: Timeline Unit Tests

**Goal:** Add unit tests for TimelineViewModel and TimelineMetrics

**Files:**
- Create: `src/SpartaCut.Tests/ViewModels/TimelineViewModelTests.cs`
- Create: `src/SpartaCut.Tests/Models/TimelineMetricsTests.cs`

### Step 1: Create TimelineMetricsTests

```csharp
using SpartaCut.Models;
using Xunit;

namespace SpartaCut.Tests.Models;

public class TimelineMetricsTests
{
    [Fact]
    public void PixelsPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var pixelsPerSecond = metrics.PixelsPerSecond;

        // Assert
        Assert.Equal(10, pixelsPerSecond); // 1000 pixels / 100 seconds = 10 px/s
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 100)]
    [InlineData(50, 500)]
    [InlineData(100, 1000)]
    public void TimeToPixel_ConvertsCorrectly(double seconds, double expectedPixel)
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var pixel = metrics.TimeToPixel(TimeSpan.FromSeconds(seconds));

        // Assert
        Assert.Equal(expectedPixel, pixel);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 10)]
    [InlineData(500, 50)]
    [InlineData(1000, 100)]
    public void PixelToTime_ConvertsCorrectly(double pixel, double expectedSeconds)
    {
        // Arrange
        var metrics = new TimelineMetrics
        {
            TotalDuration = TimeSpan.FromSeconds(100),
            TimelineWidth = 1000,
            TimelineHeight = 200
        };

        // Act
        var time = metrics.PixelToTime(pixel);

        // Assert
        Assert.Equal(expectedSeconds, time.TotalSeconds);
    }
}
```

### Step 2: Create TimelineViewModelTests

```csharp
using SpartaCut.Models;
using SpartaCut.ViewModels;
using Xunit;

namespace SpartaCut.Tests.ViewModels;

public class TimelineViewModelTests
{
    [Fact]
    public void SeekToPixel_UpdatesCurrentTime()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            TimelineHeight = 200,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(500); // Middle of timeline

        // Assert
        Assert.Equal(50, viewModel.CurrentTime.TotalSeconds);
    }

    [Fact]
    public void SeekToPixel_ClampsToZero()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(-100); // Negative position

        // Assert
        Assert.Equal(TimeSpan.Zero, viewModel.CurrentTime);
    }

    [Fact]
    public void SeekToPixel_ClampsToDuration()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            }
        };

        // Act
        viewModel.SeekToPixel(2000); // Beyond timeline

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(100), viewModel.CurrentTime);
    }

    [Fact]
    public void PlayheadPosition_ReflectsCurrentTime()
    {
        // Arrange
        var viewModel = new TimelineViewModel
        {
            TimelineWidth = 1000,
            VideoMetadata = new VideoMetadata
            {
                FilePath = "/test/video.mp4",
                Duration = TimeSpan.FromSeconds(100),
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                CodecName = "h264",
                PixelFormat = "yuv420p",
                Bitrate = 5000000
            },
            CurrentTime = TimeSpan.FromSeconds(25)
        };

        // Act
        var position = viewModel.PlayheadPosition;

        // Assert
        Assert.Equal(250, position); // 25 seconds * 10 px/s = 250 pixels
    }
}
```

### Step 3: Run tests

Run: `/usr/local/share/dotnet/dotnet test --filter "FullyQualifiedName~Timeline"`
Expected: All timeline tests pass

### Step 4: Commit

```bash
git add src/SpartaCut.Tests/ViewModels/TimelineViewModelTests.cs src/SpartaCut.Tests/Models/TimelineMetricsTests.cs
git commit -m "test: add unit tests for TimelineViewModel and TimelineMetrics"
```

---

## Task 8: Update Project Version

**Goal:** Increment version to 0.3.0 for Week 3 milestone

**Files:**
- Modify: `src/Bref/SpartaCut.csproj`
- Modify: `src/Bref/App.axaml.cs`

### Step 1: Update version in SpartaCut.csproj

```xml
<!-- Version Information -->
<Version>0.3.0</Version>
<AssemblyVersion>0.3.0</AssemblyVersion>
<FileVersion>0.3.0</FileVersion>
<InformationalVersion>0.3.0</InformationalVersion>
```

### Step 2: Update About dialog

In App.axaml.cs, update version text:
```csharp
Text = "Version 0.3.0 - Week 3 Complete",
```

### Step 3: Build to verify

Run: `/usr/local/share/dotnet/dotnet build`
Expected: Build succeeds

### Step 4: Commit and tag

```bash
git add src/Bref/SpartaCut.csproj src/Bref/App.axaml.cs
git commit -m "chore: bump version to 0.3.0 for Week 3 milestone"
git tag v0.3.0
```

---

## Task 9: Update Development Log

**Goal:** Document Week 3 completion in development log

**Files:**
- Modify: `docs/development-log.md`

### Step 1: Add Week 3 section

```markdown
## Week 3: Timeline UI & Thumbnails

**Date:** 2025-11-04
**Status:** ✅ Complete
**Version:** 0.3.0

### Objectives
- Implement ThumbnailGenerator for video frame extraction
- Create custom TimelineControl with SkiaSharp rendering
- Display waveform visualization on timeline
- Display video thumbnails at 5-second intervals
- Render time ruler with markers
- Implement playhead with click-to-seek
- Integrate timeline into MainWindow

### Completed Tasks

1. **Thumbnail Data Models** - ThumbnailData and TimelineMetrics
2. **ThumbnailGenerator Service (TDD)** - FFmpeg-based thumbnail extraction
3. **TimelineViewModel** - MVVM state management for timeline
4. **TimelineControl XAML** - Custom control structure
5. **TimelineControl Rendering** - SkiaSharp graphics (waveform, thumbnails, ruler, playhead)
6. **MainWindow Integration** - Timeline display below video info
7. **Timeline Unit Tests** - ViewModel and metrics tests
8. **Version Update** - Bumped to 0.3.0
9. **Documentation** - Updated development log

### Key Achievements

- ✅ Custom Avalonia control with SkiaSharp rendering
- ✅ Video thumbnail extraction at 5-second intervals
- ✅ Waveform visualization overlaid on timeline
- ✅ Time ruler with adaptive tick marks
- ✅ Interactive playhead with click-and-drag seeking
- ✅ MVVM architecture with TimelineViewModel
- ✅ Unit tests for coordinate conversion logic

### Technical Highlights

**ThumbnailGenerator:**
- Uses FFmpeg.AutoGen to seek and extract frames
- Scales frames to 160x90 thumbnails
- Converts RGB24 frames to JPEG using SkiaSharp
- Generates thumbnails at 5-second intervals

**TimelineControl:**
- Custom rendering using SkiaSharp canvas
- Three visual layers: waveform (top 30%), thumbnails (middle 50%), ruler (bottom 20%)
- Playhead overlay with red line and triangle handle
- Click-to-seek and drag interactions

**TimelineViewModel:**
- Observable properties for CurrentTime, Thumbnails, VideoMetadata
- TimelineMetrics for pixel/time coordinate conversion
- RelayCommand for SeekToPixel with bounds clamping
- MVVM pattern using CommunityToolkit.Mvvm

**Coordinate System:**
- PixelsPerSecond = TimelineWidth / TotalDuration
- TimeToPixel(time) = time.TotalSeconds * PixelsPerSecond
- PixelToTime(pixel) = TimeSpan.FromSeconds(pixel / PixelsPerSecond)

### Commits

1. `feat: add ThumbnailData and TimelineMetrics models`
2. `feat: implement ThumbnailGenerator using FFmpeg and SkiaSharp`
3. `feat: add TimelineViewModel for timeline state management`
4. `feat: add TimelineControl XAML structure`
5. `feat: implement TimelineControl rendering with SkiaSharp`
6. `feat: integrate TimelineControl into MainWindow`
7. `test: add unit tests for TimelineViewModel and TimelineMetrics`
8. `chore: bump version to 0.3.0 for Week 3 milestone`
9. `docs: update development log for Week 3 completion`

### Testing

**Unit Tests:**
- TimelineMetrics coordinate conversion tests
- TimelineViewModel seek and clamp tests
- All tests passing

**Manual Testing:**
- Verified timeline renders waveform correctly
- Confirmed thumbnails display at proper intervals
- Tested click-to-seek functionality
- Verified playhead position updates

### Time Estimate vs Actual

- **Estimated:** 30 hours
- **Actual:** TBD (to be filled after completion)

### Next Steps

Week 4: Frame Cache & Preview (see roadmap)
```

### Step 2: Commit

```bash
git add docs/development-log.md
git commit -m "docs: update development log for Week 3 completion"
```

---

## Verification Checklist

Before considering Week 3 complete, verify:

- [ ] All 9 tasks committed
- [ ] All unit tests pass: `dotnet test`
- [ ] Project builds without errors: `dotnet build`
- [ ] Manual test: Load MP4 file shows timeline with waveform, thumbnails, ruler, playhead
- [ ] Click on timeline moves playhead
- [ ] Drag playhead works smoothly
- [ ] Version updated to 0.3.0 in .csproj and About dialog
- [ ] Git tag v0.3.0 created
- [ ] Development log updated

**Final Command:**
```bash
git push origin dev && git push origin v0.3.0
```

---

## Notes for Engineer

**SkiaSharp Rendering in Avalonia:**
- Use `ISkiaSharpApiLeaseFeature` to get SKCanvas from DrawingContext
- Call `InvalidateVisual()` to trigger re-render
- Rendering happens in `Render(DrawingContext context)` override

**FFmpeg Frame Extraction:**
- Use `av_seek_frame()` with `AVSEEK_FLAG_BACKWARD` to seek to keyframes
- Flush codec buffers after seeking with `avcodec_flush_buffers()`
- Scale frames with `sws_getContext()` and `sws_scale()`
- Convert to JPEG with SkiaSharp for compact storage

**Performance Considerations:**
- Thumbnail generation can be slow for long videos - consider progress reporting
- Waveform rendering uses sampling (samplesPerPixel) to avoid drawing every peak
- Timeline rendering is fast with SkiaSharp hardware acceleration

**Common Issues:**
- **Pointer events not working:** Ensure `PointerPressed/Moved/Released` handlers are attached and `e.Handled = true`
- **Timeline not rendering:** Check that DataContext is set to TimelineViewModel
- **Thumbnails not appearing:** Verify JPEG encoding works and ImageData is not empty
- **Waveform flat:** Ensure Waveform.Peaks array is populated from Week 2

---

## Summary

**Week 3 Deliverable:** Custom timeline control displaying waveform visualization, video thumbnails, time ruler with markers, and interactive playhead with click-to-seek functionality.

**Key Components:**
- ThumbnailGenerator (FFmpeg frame extraction)
- TimelineControl (SkiaSharp custom rendering)
- TimelineViewModel (MVVM state management)
- TimelineMetrics (coordinate conversion logic)
- Unit tests (ViewModel and metrics)

**Testing Strategy:** TDD for service layer and ViewModel, visual/manual testing for rendering, unit tests for coordinate math.

**Estimated Effort:** 30 hours over 9 tasks.
