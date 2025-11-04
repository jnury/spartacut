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
using Bref.Models;
using Bref.ViewModels;
using SkiaSharp;

namespace Bref.Controls;

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

        context.Custom(new TimelineRenderOperation(Bounds, _viewModel));
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
            using var paint = new SKPaint
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
