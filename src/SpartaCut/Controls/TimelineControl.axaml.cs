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
using SpartaCut.Core.Models;
using SpartaCut.Core.ViewModels;
using Serilog;
using SkiaSharp;

namespace SpartaCut.Controls;

public partial class TimelineControl : UserControl
{
    private TimelineViewModel? _viewModel;
    private bool _isDragging;
    private Point? _pointerDownPosition;
    private const double DragThreshold = 5.0; // pixels
    private const double EdgeHitZone = 10.0; // pixels - hit zone for selection edges
    private bool _isRulerDrag; // Track if dragging in ruler area
    private bool _isDraggingStartEdge; // Track if dragging selection start edge
    private bool _isDraggingEndEdge; // Track if dragging selection end edge
    private TimeSpan _fixedEdgeTime; // Store the non-dragged edge time during resize
    private bool _isHoveringDeleteIcon; // Track if hovering over delete icon
    private const double DeleteIconSize = 20.0; // pixels
    private const double DeleteIconHitZone = 24.0; // pixels - slightly larger for easier clicking
    private Point? _currentPointerPosition; // Track current pointer position for hover detection

    public TimelineControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Check if a Y coordinate is in the ruler (time markers) area
    /// </summary>
    private bool IsInRulerArea(double y)
    {
        if (Bounds.Height == 0) return false;

        // Ruler is the bottom 20% of the timeline
        var waveformHeight = Bounds.Height * 0.25;
        var thumbnailHeight = Bounds.Height * 0.55;
        var rulerStart = waveformHeight + thumbnailHeight;

        return y >= rulerStart;
    }

    /// <summary>
    /// Get the center position of the delete icon for the current selection.
    /// Smart positioning: outside selection by default, inside when too close to timeline edges.
    /// </summary>
    private Point? GetDeleteIconPosition()
    {
        if (_viewModel?.Selection.IsActive != true || _viewModel.Metrics == null) return null;

        const double iconRadius = 10.0; // Half of DeleteIconSize (20px)
        const double borderPadding = 10.0; // Minimum distance from timeline border
        const double selectionPadding = 10.0; // Distance from selection edge

        var timelineWidth = Bounds.Width;
        var selectionLeftX = _viewModel.SelectionNormalizedStartPixel;
        var selectionRightX = selectionLeftX + _viewModel.SelectionWidth;

        // Determine if selection is in left or right half of video
        var selectionStartTime = _viewModel.Selection.NormalizedStart;
        var videoMidpoint = _viewModel.Metrics.TotalDuration.Ticks / 2.0;
        var isLeftHalf = selectionStartTime.Ticks < videoMidpoint;

        double iconX;

        if (isLeftHalf)
        {
            // Selection in left half - prefer icon on RIGHT of selection (outside)
            var preferredX = selectionRightX + selectionPadding + iconRadius;
            var maxAllowedX = timelineWidth - borderPadding - iconRadius;

            if (preferredX > maxAllowedX)
            {
                // Too close to right edge - switch to inside left of selection
                iconX = selectionRightX - selectionPadding - iconRadius;
            }
            else
            {
                iconX = preferredX;
            }
        }
        else
        {
            // Selection in right half - prefer icon on LEFT of selection (outside)
            var preferredX = selectionLeftX - selectionPadding - iconRadius;
            var minAllowedX = borderPadding + iconRadius;

            if (preferredX < minAllowedX)
            {
                // Too close to left edge - switch to inside right of selection
                iconX = selectionLeftX + selectionPadding + iconRadius;
            }
            else
            {
                iconX = preferredX;
            }
        }

        // Icon position: top of timeline
        var iconY = iconRadius + 5.0; // 5px from top edge

        return new Point(iconX, iconY);
    }

    /// <summary>
    /// Check if a point is within the delete icon hit zone
    /// </summary>
    private bool IsPointInDeleteIcon(Point point)
    {
        var iconPosition = GetDeleteIconPosition();
        if (iconPosition == null) return false;

        var dx = point.X - iconPosition.Value.X;
        var dy = point.Y - iconPosition.Value.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        return distance <= (DeleteIconHitZone / 2);
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

        // Check if clicking on delete icon first
        if (_viewModel.Selection.IsActive && IsPointInDeleteIcon(point))
        {
            // Raise delete requested event for MainWindow to handle
            _viewModel.RaiseDeleteRequested();
            e.Handled = true;
            return;
        }

        _pointerDownPosition = point;
        _isDragging = false;
        _isDraggingStartEdge = false;
        _isDraggingEndEdge = false;
        _isRulerDrag = IsInRulerArea(point.Y);

        // If in ruler area, immediately seek to position
        if (_isRulerDrag)
        {
            _viewModel.SeekToPixel(point.X);
            _viewModel.ClearSelectionCommand.Execute(null);
        }
        else if (_viewModel.Selection.IsActive)
        {
            // Check if clicking on selection edges for resizing
            var startPixel = _viewModel.SelectionNormalizedStartPixel;
            var endPixel = startPixel + _viewModel.SelectionWidth;

            var distanceToStart = Math.Abs(point.X - startPixel);
            var distanceToEnd = Math.Abs(point.X - endPixel);

            if (distanceToStart <= EdgeHitZone)
            {
                // Dragging start edge
                _isDraggingStartEdge = true;
                _fixedEdgeTime = _viewModel.Selection.NormalizedEnd; // Fix the end
            }
            else if (distanceToEnd <= EdgeHitZone)
            {
                // Dragging end edge
                _isDraggingEndEdge = true;
                _fixedEdgeTime = _viewModel.Selection.NormalizedStart; // Fix the start
            }
            // Otherwise, will handle as normal click/drag in OnPointerMoved/Released
        }

        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel?.VideoMetadata == null)
            return;

        try
        {
            var point = e.GetPosition(this);
            _currentPointerPosition = point;

            // Update hover state for delete icon
            var wasHovering = _isHoveringDeleteIcon;
            _isHoveringDeleteIcon = _viewModel.Selection.IsActive && IsPointInDeleteIcon(point);

            // Invalidate if hover state changed
            if (wasHovering != _isHoveringDeleteIcon)
            {
                InvalidateVisual();
            }

            // Only handle drag if pointer was pressed
            if (_pointerDownPosition == null)
                return;

            if (_isDraggingStartEdge || _isDraggingEndEdge)
            {
                // Dragging selection edge - resize selection and update playhead for preview
                var metrics = _viewModel.Metrics;
                if (metrics != null)
                {
                    // Clamp pointer X to timeline bounds BEFORE converting to time
                    // This prevents selection from expanding when dragging outside timeline
                    var clampedX = Math.Clamp(point.X, 0, Bounds.Width);

                    var draggedTime = metrics.PixelToTime(clampedX);

                    // Additional time-based clamping for safety
                    var maxDuration = metrics.TotalDuration;
                    draggedTime = TimeSpan.FromTicks(Math.Clamp(draggedTime.Ticks, 0, maxDuration.Ticks));

                    // Resize selection with fixed and dragged edges
                    _viewModel.ResizeSelection(_fixedEdgeTime, draggedTime);

                    // Seek playhead to dragged position for preview
                    _viewModel.SeekToPixel(clampedX);
                    InvalidateVisual();
                }
            }
            else if (_isRulerDrag)
            {
                // Dragging in ruler area - update playhead position
                _viewModel.SeekToPixel(point.X);
                InvalidateVisual();
            }
            else
            {
                // Dragging in main timeline area - create/update selection
                var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

                if (!_isDragging && distance > DragThreshold)
                {
                    // Start selection - clamp to timeline bounds
                    _isDragging = true;
                    var clampedStartX = Math.Clamp(_pointerDownPosition.Value.X, 0, Bounds.Width);
                    _viewModel.StartSelectionCommand.Execute(clampedStartX);
                }

                if (_isDragging)
                {
                    // Update selection - clamp to timeline bounds
                    var clampedX = Math.Clamp(point.X, 0, Bounds.Width);
                    _viewModel.UpdateSelectionCommand.Execute(clampedX);
                    InvalidateVisual();
                }
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

        if (_isDraggingStartEdge || _isDraggingEndEdge)
        {
            // Edge dragging completed - check if edges met (cancel if so)
            var selectionWidth = _viewModel.SelectionWidth;
            if (selectionWidth < DragThreshold)
            {
                // Edges met - cancel selection
                _viewModel.ClearSelectionCommand.Execute(null);
            }
            // Otherwise keep the resized selection
        }
        else if (_isRulerDrag)
        {
            // Ruler drag completed - playhead already moved during drag
            // Just cleanup
        }
        else
        {
            // Click or drag in main timeline area
            var distance = Math.Abs(point.X - _pointerDownPosition.Value.X);

            if (distance <= DragThreshold)
            {
                // Single click
                var playheadPixel = _viewModel.PlayheadPosition;
                var clickX = point.X;

                // Check if clicking exactly on playhead (within threshold)
                if (Math.Abs(clickX - playheadPixel) <= DragThreshold)
                {
                    // Click on playhead - do nothing
                }
                else if (playheadPixel <= 1.0) // Playhead at position 0
                {
                    // Playhead at 0 - seek to click position
                    _viewModel.SeekToPixel(clickX);
                    _viewModel.ClearSelectionCommand.Execute(null);
                }
                else if (clickX < playheadPixel)
                {
                    // Click left of playhead - create selection from 0 to playhead
                    var metrics = _viewModel.Metrics;
                    if (metrics != null)
                    {
                        var playheadTime = metrics.PixelToTime(playheadPixel);
                        _viewModel.CreateSelection(TimeSpan.Zero, playheadTime);
                    }
                }
                else // clickX > playheadPixel
                {
                    // Click right of playhead - create selection from playhead to end
                    var metrics = _viewModel.Metrics;
                    if (metrics != null)
                    {
                        var playheadTime = metrics.PixelToTime(playheadPixel);
                        var endTime = metrics.TotalDuration;
                        _viewModel.CreateSelection(playheadTime, endTime);
                    }
                }
            }
            else if (_viewModel.Selection.IsValid)
            {
                // Valid selection drag completed
                // Selection remains active for Delete operation
            }
            else
            {
                // Invalid selection (too small)
                _viewModel.ClearSelectionCommand.Execute(null);
            }
        }

        _pointerDownPosition = null;
        _isDragging = false;
        _isDraggingStartEdge = false;
        _isDraggingEndEdge = false;
        _isRulerDrag = false;
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_viewModel?.VideoMetadata == null || _viewModel.Metrics == null)
            return;

        var renderBounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.Custom(new TimelineRenderOperation(renderBounds, _viewModel, _isHoveringDeleteIcon));
    }

    private class TimelineRenderOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly TimelineViewModel _viewModel;
        private readonly bool _isHoveringDeleteIcon;

        public TimelineRenderOperation(Rect bounds, TimelineViewModel viewModel, bool isHoveringDeleteIcon)
        {
            _bounds = bounds;
            _viewModel = viewModel;
            _isHoveringDeleteIcon = isHoveringDeleteIcon;
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

            // Render delete icon (on top of selection)
            if (viewModel.Selection.IsActive)
            {
                RenderDeleteIcon(canvas, viewModel, height);
            }

            // Render playhead
            RenderPlayhead(canvas, viewModel, height);
        }

        private static SKTypeface? _fontAwesomeTypeface;

        private static SKTypeface GetFontAwesomeTypeface()
        {
            if (_fontAwesomeTypeface == null)
            {
                try
                {
                    // Load Font Awesome from embedded resource
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var resourceName = "avares://SpartaCut/Assets/Fonts/fa-solid-900.ttf";

                    using var stream = Avalonia.Platform.AssetLoader.Open(new Uri(resourceName));
                    _fontAwesomeTypeface = SKTypeface.FromStream(stream);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load Font Awesome font");
                    // Fallback to default font
                    _fontAwesomeTypeface = SKTypeface.Default;
                }
            }
            return _fontAwesomeTypeface;
        }

        private void RenderDeleteIcon(SKCanvas canvas, TimelineViewModel viewModel, float height)
        {
            if (!viewModel.Selection.IsActive || viewModel.Metrics == null) return;

            const float iconRadius = 10f; // Half of DeleteIconSize (20px)
            const float borderPadding = 10f; // Minimum distance from timeline border
            const float selectionPadding = 10f; // Distance from selection edge

            var timelineWidth = (float)viewModel.TimelineWidth;
            var selectionLeftX = (float)viewModel.SelectionNormalizedStartPixel;
            var selectionRightX = selectionLeftX + (float)viewModel.SelectionWidth;

            // Determine if selection is in left or right half of video
            var selectionStartTime = viewModel.Selection.NormalizedStart;
            var videoMidpoint = viewModel.Metrics.TotalDuration.Ticks / 2.0;
            var isLeftHalf = selectionStartTime.Ticks < videoMidpoint;

            float iconCenterX;

            if (isLeftHalf)
            {
                // Selection in left half - prefer icon on RIGHT of selection (outside)
                var preferredX = selectionRightX + selectionPadding + iconRadius;
                var maxAllowedX = timelineWidth - borderPadding - iconRadius;

                if (preferredX > maxAllowedX)
                {
                    // Too close to right edge - switch to inside left of selection
                    iconCenterX = selectionRightX - selectionPadding - iconRadius;
                }
                else
                {
                    iconCenterX = preferredX;
                }
            }
            else
            {
                // Selection in right half - prefer icon on LEFT of selection (outside)
                var preferredX = selectionLeftX - selectionPadding - iconRadius;
                var minAllowedX = borderPadding + iconRadius;

                if (preferredX < minAllowedX)
                {
                    // Too close to left edge - switch to inside right of selection
                    iconCenterX = selectionLeftX + selectionPadding + iconRadius;
                }
                else
                {
                    iconCenterX = preferredX;
                }
            }

            // Icon position: top of timeline
            var iconCenterY = iconRadius + 5f; // 5px from top edge

            // Draw circular background
            using var bgPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = _isHoveringDeleteIcon
                    ? SKColor.Parse("#D7641C") // Orange on hover
                    : SKColor.Parse("#888888")  // Grey default
            };
            canvas.DrawCircle(iconCenterX, iconCenterY, iconRadius, bgPaint);

            // Draw trash icon using Font Awesome
            var typeface = GetFontAwesomeTypeface();
            using var iconPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                TextSize = 12f,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            // Font Awesome trash-can icon unicode
            const string trashIcon = "\uf2ed";

            // Draw icon text centered
            var textBounds = new SKRect();
            iconPaint.MeasureText(trashIcon, ref textBounds);
            var textY = iconCenterY - textBounds.MidY;

            canvas.DrawText(trashIcon, iconCenterX, textY, iconPaint);
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

            // Calculate selection height (exclude ruler area at bottom)
            var waveformHeight = height * 0.25f;
            var thumbnailHeight = height * 0.55f;
            var selectionHeight = waveformHeight + thumbnailHeight; // Don't cover ruler

            var selectionRect = new SKRect(
                (float)viewModel.SelectionNormalizedStartPixel,
                0,
                (float)(viewModel.SelectionNormalizedStartPixel + viewModel.SelectionWidth),
                selectionHeight
            );

            // Semi-transparent blue overlay
            using var selectionPaint = new SKPaint
            {
                Color = new SKColor(100, 150, 255, 80), // Light blue, 30% opacity
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(selectionRect, selectionPaint);

            // Selection border (thin like playhead)
            using var borderPaint = new SKPaint
            {
                Color = new SKColor(100, 150, 255, 200), // Light blue, 80% opacity
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };
            canvas.DrawRect(selectionRect, borderPaint);
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
        }
    }
}
