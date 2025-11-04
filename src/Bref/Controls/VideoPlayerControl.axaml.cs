using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Bref.Models;
using SkiaSharp;

namespace Bref.Controls;

/// <summary>
/// Video player control that displays VideoFrame using SkiaSharp rendering.
/// Automatically handles aspect ratio preservation and letterboxing.
/// </summary>
public partial class VideoPlayerControl : UserControl
{
    private VideoFrame? _currentFrame;
    private SKBitmap? _currentBitmap;
    private readonly Queue<SKBitmap> _bitmapHistory = new Queue<SKBitmap>();
    private const int MaxBitmapHistory = 10; // Keep more bitmaps to prevent disposal during render
    private readonly object _bitmapLock = new object();

    public VideoPlayerControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Displays a video frame.
    /// Thread-safe - can be called from any thread.
    /// NOTE: Frame ownership remains with the caller (e.g., FrameCache).
    /// Only the bitmap created from the frame is disposed by this control.
    /// </summary>
    public void DisplayFrame(VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (Dispatcher.UIThread.CheckAccess())
        {
            // Already on UI thread, update synchronously for immediate rendering
            UpdateFrame(frame);
        }
        else
        {
            // Post to UI thread
            Dispatcher.UIThread.Post(() => UpdateFrame(frame));
        }
    }

    private void UpdateFrame(VideoFrame frame)
    {
        lock (_bitmapLock)
        {
            // Add current bitmap to history before replacing
            if (_currentBitmap != null)
            {
                _bitmapHistory.Enqueue(_currentBitmap);

                // Dequeue old bitmaps if we have too many in history
                // Don't dispose them - let GC/finalizers handle cleanup to avoid race conditions
                while (_bitmapHistory.Count > MaxBitmapHistory)
                {
                    _bitmapHistory.Dequeue();
                }
            }

            _currentFrame = frame;
            _currentBitmap = frame.ToBitmap();
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Clears the current frame.
    /// </summary>
    public void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_bitmapLock)
            {
                // Clear bitmap references (let GC handle disposal)
                _bitmapHistory.Clear();
                _currentFrame = null;
                _currentBitmap = null;
            }

            InvalidateVisual();
        });
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var renderBounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        // Capture bitmap under lock to prevent disposal during render
        SKBitmap? bitmapToRender;
        TimeSpan? frameTime = null;
        lock (_bitmapLock)
        {
            bitmapToRender = _currentBitmap;
            frameTime = _currentFrame?.TimePosition;
        }

        if (bitmapToRender == null)
        {
            context.Custom(new PlaceholderRenderOperation(renderBounds));
            return;
        }

        context.Custom(new VideoFrameRenderOperation(renderBounds, bitmapToRender));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        lock (_bitmapLock)
        {
            // Clear bitmap references (let GC handle disposal)
            _bitmapHistory.Clear();
            _currentFrame = null;
            _currentBitmap = null;
        }
    }

    private class VideoFrameRenderOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly SKBitmap _bitmap;

        public VideoFrameRenderOperation(Rect bounds, SKBitmap bitmap)
        {
            _bounds = bounds;
            _bitmap = bitmap;
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

            RenderFrame(canvas, _bitmap, _bounds);
        }

        private void RenderFrame(SKCanvas canvas, SKBitmap bitmap, Rect bounds)
        {
            var width = (float)bounds.Width;
            var height = (float)bounds.Height;

            // Clear to black
            canvas.Clear(SKColors.Black);

            // Calculate aspect ratio preserving rectangle
            var videoAspect = (float)bitmap.Width / bitmap.Height;
            var viewAspect = width / height;

            SKRect destRect;

            if (videoAspect > viewAspect)
            {
                // Video wider than view - fit width
                var scaledHeight = width / videoAspect;
                var yOffset = (height - scaledHeight) / 2;
                destRect = new SKRect(0, yOffset, width, yOffset + scaledHeight);
            }
            else
            {
                // Video taller than view - fit height
                var scaledWidth = height * videoAspect;
                var xOffset = (width - scaledWidth) / 2;
                destRect = new SKRect(xOffset, 0, xOffset + scaledWidth, height);
            }

            // Render bitmap with high quality
            var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.DrawBitmap(bitmap, destRect, paint);
        }
    }

    private class PlaceholderRenderOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;

        public PlaceholderRenderOperation(Rect bounds)
        {
            _bounds = bounds;
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

            // Clear to black (since UserControl Background is Transparent)
            canvas.Clear(SKColors.Black);

            // Draw placeholder text
            using var paint = new SKPaint
            {
                Color = SKColor.Parse("#666666"),
                TextSize = 16,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            var text = "No video loaded";
            var x = (float)_bounds.Width / 2;
            var y = (float)_bounds.Height / 2;

            canvas.DrawText(text, x, y, paint);
        }
    }
}
