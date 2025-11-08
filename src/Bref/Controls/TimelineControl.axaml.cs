using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Bref.Core.Models;
using Bref.Core.ViewModels;
using Serilog;
using SkiaSharp;

namespace Bref.Controls;

public partial class TimelineControl : UserControl
{
    private TimelineViewModel? _viewModel;
    private bool _isDragging;
    private Point? _pointerDownPosition;
    private const double DragThreshold = 5.0; // pixels

    public TimelineControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.SegmentsChanged -= OnSegmentsChanged;
        }

        _viewModel = DataContext as TimelineViewModel;

        // Subscribe to new ViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.SegmentsChanged += OnSegmentsChanged;
        }

        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Re-render when relevant properties change
        InvalidateVisual();
    }

    private void OnSegmentsChanged(object? sender, EventArgs e)
    {
        // Force re-render when segments change (delete/undo/redo)
        Dispatcher.UIThread.Post(InvalidateVisual);
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
        if (_viewModel?.VideoMetadata == null)
            return;

        var point = e.GetPosition(this);
        _pointerDownPosition = point;
        _isDragging = false; // Don't start dragging yet
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pointerDownPosition == null || _viewModel?.VideoMetadata == null)
            return;

        try
        {
            var point = e.GetPosition(this);

            // Check if moved beyond threshold (selection drag vs. seek click)
            var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

            if (!_isDragging && distance > DragThreshold)
            {
                // Start selection
                _isDragging = true;
                _viewModel.StartSelectionCommand.Execute(_pointerDownPosition.Value.X);
            }

            if (_isDragging)
            {
                // Update selection
                _viewModel.UpdateSelectionCommand.Execute(point.X);
                InvalidateVisual();
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during timeline drag");
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewModel == null || _pointerDownPosition == null)
            return;

        var point = e.GetPosition(this);
        var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

        if (distance <= DragThreshold)
        {
            // Single click - seek to position
            _viewModel.SeekToPixel(point.X);
            _viewModel.ClearSelectionCommand.Execute(null);
        }
        else if (_viewModel.Selection.IsValid)
        {
            // Valid selection completed
            // Selection remains active for Delete operation
        }
        else
        {
            // Invalid selection (too small)
            _viewModel.ClearSelectionCommand.Execute(null);
        }

        _pointerDownPosition = null;
        _isDragging = false;
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_viewModel?.VideoMetadata == null || _viewModel.Metrics == null)
            return;

        var renderBounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(new TimelineRenderOperation(renderBounds, _viewModel));
    }

    private class TimelineRenderOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly TimelineViewModel _viewModel;

        public TimelineRenderOperation(Rect bounds, TimelineViewModel viewModel)
        {
            _bounds = bounds;
            _viewModel = viewModel;
        }

        public void Dispose() { }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            RenderTimeline(canvas, _viewModel, _bounds);
        }

        private void RenderTimeline(SKCanvas canvas, TimelineViewModel viewModel, Rect bounds)
        {
            var width = (float)bounds.Width;
            var height = (float)bounds.Height;

            // Removed: Log on every render (too verbose)
            // Log.Information("RenderTimeline: Width={Width:F0}px, Metrics.Duration={MetricsDuration:F2}s, Video.Duration={VideoDuration:F2}s",
            //     width, metricsDuration, videoDuration);

            // Clear background
            canvas.Clear(SKColor.Parse("#2D2D2D"));

            // Define regions (optimized for 150px height with smaller thumbnails)
            var waveformHeight = height * 0.25f;  // ~38px for waveform
            var thumbnailHeight = height * 0.55f; // ~82px for thumbnails (fit 100x56)
            var rulerHeight = height * 0.2f;      // ~30px for ruler

            // Render waveform
            RenderWaveform(canvas, viewModel, 0, waveformHeight, width);

            // Render thumbnails
            RenderThumbnails(canvas, viewModel, waveformHeight, thumbnailHeight, width);

            // Render time ruler
            RenderTimeRuler(canvas, viewModel, waveformHeight + thumbnailHeight, rulerHeight, width);

            // Note: No deleted regions rendering - timeline contracts to show only kept segments

            // Render selection highlight
            RenderSelection(canvas, viewModel, height);

            // Render playhead
            RenderPlayhead(canvas, viewModel, height);
        }

        private void RenderWaveform(SKCanvas canvas, TimelineViewModel viewModel, float y, float height, float width)
        {
            if (viewModel.VideoMetadata?.Waveform?.Peaks == null) return;

            var waveform = viewModel.VideoMetadata.Waveform;
            var peaks = waveform.Peaks;
            var metrics = viewModel.Metrics;
            if (metrics == null) return;

            using var paint = new SKPaint
            {
                Color = SKColor.Parse("#007ACC"),
                StrokeWidth = 1,
                IsAntialias = true
            };

            // Draw waveform peaks mapped to virtual timeline
            var centerY = y + height / 2;
            var videoDuration = viewModel.VideoMetadata.Duration;

            // For each pixel, map virtual time to source time and sample waveform
            for (int x = 0; x < width; x++)
            {
                // Convert pixel to virtual time
                var virtualTime = metrics.PixelToTime(x);

                // Convert virtual time to source time
                var sourceTime = viewModel.VirtualToSourceTime(virtualTime);

                // Calculate waveform sample index based on source time
                var sourceProgress = sourceTime.TotalSeconds / videoDuration.TotalSeconds;
                var sampleIndex = (int)(sourceProgress * (peaks.Length / 2)) * 2; // Peaks are min/max pairs

                if (sampleIndex >= 0 && sampleIndex + 1 < peaks.Length)
                {
                    var min = peaks[sampleIndex];
                    var max = peaks[sampleIndex + 1];

                    var minY = centerY - (min * height / 2);
                    var maxY = centerY - (max * height / 2);

                    canvas.DrawLine(x, (float)minY, x, (float)maxY, paint);
                }
            }
        }

        private void RenderThumbnails(SKCanvas canvas, TimelineViewModel viewModel, float y, float height, float width)
        {
            if (!viewModel.Thumbnails.Any()) return;

            var metrics = viewModel.Metrics!;
            using var thumbnailPaint = new SKPaint { IsAntialias = true };

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
                    using var borderPaint = new SKPaint
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
            using var bgPaint = new SKPaint { Color = SKColor.Parse("#1E1E1E") };
            canvas.DrawRect(0, y, width, height, bgPaint);

            // Draw time markers
            using var textPaint = new SKPaint
            {
                Color = SKColor.Parse("#CCCCCC"),
                TextSize = 12,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            using var tickPaint = new SKPaint
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

        private void RenderDeletedRegions(SKCanvas canvas, TimelineViewModel viewModel, float height)
        {
            // Note: This method is no longer called - timeline contracts instead of showing overlays
            var deletedRegions = viewModel.GetDeletedRegions();

            if (deletedRegions.Count == 0 || viewModel.VideoMetadata == null)
                return;

            var videoDuration = viewModel.VideoMetadata.Duration;
            if (videoDuration.TotalSeconds == 0)
                return;

            // Render deleted regions as red semi-transparent overlays
            using var deletedPaint = new SKPaint
            {
                Color = new SKColor(255, 50, 50, 100), // Red with 40% opacity
                Style = SKPaintStyle.Fill
            };

            using var borderPaint = new SKPaint
            {
                Color = new SKColor(200, 0, 0, 150), // Dark red border
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };

            var width = (float)viewModel.TimelineWidth;

            foreach (var (start, end) in deletedRegions)
            {
                // Convert source times to pixel positions (using full video duration)
                var startPixel = (float)(start.TotalSeconds / videoDuration.TotalSeconds * width);
                var endPixel = (float)(end.TotalSeconds / videoDuration.TotalSeconds * width);

                var deletedRect = new SKRect(startPixel, 0, endPixel, height);

                // Draw red overlay
                canvas.DrawRect(deletedRect, deletedPaint);

                // Draw border
                canvas.DrawRect(deletedRect, borderPaint);
            }
        }

        private void RenderSelection(SKCanvas canvas, TimelineViewModel viewModel, float height)
        {
            if (!viewModel.Selection.IsActive)
                return;

            var selectionRect = new SKRect(
                (float)viewModel.SelectionNormalizedStartPixel,
                0,
                (float)(viewModel.SelectionNormalizedStartPixel + viewModel.SelectionWidth),
                height
            );

            // Semi-transparent blue overlay
            using var selectionPaint = new SKPaint
            {
                Color = new SKColor(100, 150, 255, 80), // Light blue, 30% opacity
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(selectionRect, selectionPaint);

            // Selection border
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(100, 150, 255, 200), // Light blue, 80% opacity
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            canvas.DrawRect(selectionRect, borderPaint);

            // Draw handles (for visual clarity)
            DrawSelectionHandle(canvas, selectionRect.Left, height);
            DrawSelectionHandle(canvas, selectionRect.Right, height);
        }

        private void DrawSelectionHandle(SKCanvas canvas, float x, float height)
        {
            var handleRect = new SKRect(x - 5, 0, x + 5, height);
            using var handlePaint = new SKPaint
            {
                Color = new SKColor(100, 150, 255, 255),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(handleRect, handlePaint);
        }

        private void RenderPlayhead(SKCanvas canvas, TimelineViewModel viewModel, float height)
        {
            var x = (float)viewModel.PlayheadPosition;

            // Draw playhead line
            using var linePaint = new SKPaint
            {
                Color = SKColor.Parse("#FF0000"),
                StrokeWidth = 2,
                IsAntialias = true
            };
            canvas.DrawLine(x, 0, x, height, linePaint);

            // Draw playhead handle (triangle at top)
            using var handlePaint = new SKPaint
            {
                Color = SKColor.Parse("#FF0000"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            using var handlePath = new SKPath();
            handlePath.MoveTo(x, 0);
            handlePath.LineTo(x - 8, 15);
            handlePath.LineTo(x + 8, 15);
            handlePath.Close();
            canvas.DrawPath(handlePath, handlePaint);
        }
    }
}
