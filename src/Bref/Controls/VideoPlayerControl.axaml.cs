using System;
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

    public VideoPlayerControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Displays a video frame.
    /// Thread-safe - can be called from any thread.
    /// </summary>
    public void DisplayFrame(VideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        Dispatcher.UIThread.Post(() =>
        {
            // Dispose previous frame and bitmap
            _currentFrame?.Dispose();
            _currentBitmap?.Dispose();

            _currentFrame = frame;
            _currentBitmap = frame.ToBitmap();

            PlaceholderText.IsVisible = false;
            InvalidateVisual();
        });
    }

    /// <summary>
    /// Clears the current frame.
    /// </summary>
    public void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _currentFrame?.Dispose();
            _currentBitmap?.Dispose();

            _currentFrame = null;
            _currentBitmap = null;

            PlaceholderText.IsVisible = true;
            InvalidateVisual();
        });
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_currentBitmap == null)
            return;

        var renderBounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(new VideoFrameRenderOperation(renderBounds, _currentBitmap));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Cleanup on detach
        _currentFrame?.Dispose();
        _currentBitmap?.Dispose();
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
}
