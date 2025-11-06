using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Bref.Controls;
using Bref.FFmpeg;
using Bref.Models;
using Bref.Services;
using Bref.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bref.Views;

public partial class MainWindow : Window
{
    private FrameCache? _frameCache;
    private TimelineViewModel? _timelineViewModel;
    private MainWindowViewModel? _viewModel;
    private string? _currentVideoPath;
    private double _lastRegenerationWidth = 0;

    public MainWindow()
    {
        InitializeComponent();

        // Subscribe to window size changes for thumbnail regeneration
        this.PropertyChanged += OnWindowPropertyChanged;

        // Subscribe to key events for Space key handling
        this.KeyDown += OnWindowKeyDown;

        // Initialize ViewModel
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        // Set thumbnail regeneration callback
        _viewModel.RegenerateThumbnailsCallback = RegenerateThumbnailsAsync;

        // Wire timeline ViewModel
        if (TimelineControl != null)
        {
            TimelineControl.DataContext = _viewModel.Timeline;

            // Subscribe to segment changes to refresh timeline
            // (Thumbnails are regenerated BEFORE this event fires)
            _viewModel.Timeline.SegmentsChanged += (s, e) =>
            {
                TimelineControl.InvalidateVisual();
            };
        }

        // Initialize FFmpeg on window load
        try
        {
            FFmpegSetup.Initialize();
            Log.Information("FFmpeg initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize FFmpeg");
            Title = $"Bref - ERROR: Failed to initialize FFmpeg";
        }
    }

    private async void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && _currentVideoPath != null && _viewModel != null)
        {
            var newBounds = (Rect)e.NewValue!;
            var widthChange = Math.Abs(newBounds.Width - _lastRegenerationWidth);

            // Regenerate thumbnails if width changed significantly (>100px)
            if (widthChange > 100)
            {
                _lastRegenerationWidth = newBounds.Width;
                await RegenerateThumbnailsAsync();
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        // Clear video player (disposes SKBitmaps)
        VideoPlayer?.Clear();

        // Don't dispose FrameCache/Decoder on shutdown to avoid FFmpeg race conditions
        // The OS will reclaim all resources when process terminates
        // Disposing native FFmpeg contexts during active decoding causes crashes

        Log.Information("MainWindow closing");
    }

    /// <summary>
    /// Public method to trigger video loading from menu or keyboard shortcut.
    /// </summary>
    public void TriggerLoadVideo()
    {
        LoadVideoButton_Click(null, null!);
    }

    private void OpenVideoMenuItem_Click(object? sender, EventArgs e)
    {
        LoadVideoButton_Click(null, null!);
    }

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
            _currentVideoPath = filePath; // Store for thumbnail regeneration
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

            // Initialize frame cache first (needed for ViewModel initialization)
            _frameCache?.Dispose();
            _frameCache = new FrameCache(filePath, capacity: 60);

            // Initialize ViewModel with video metadata and frame cache
            if (_viewModel != null)
            {
                _viewModel.InitializeVideo(metadata, _frameCache);
            }

            // Update window title with video info
            UpdateWindowTitle(metadata);

            Log.Information("Successfully loaded video: {Metadata}", metadata);

            // Generate thumbnails for timeline
            try
            {
                // Calculate thumbnail count based on window width
                // Aim for thumbnails every ~100 pixels (with 100px wide thumbnails)
                var windowWidth = Width > 0 ? Width : 1200; // Default if not set
                _lastRegenerationWidth = windowWidth; // Track initial width
                var availableWidth = windowWidth - 40; // Account for margins
                var thumbnailWidth = 100;
                var thumbnailCount = (int)(availableWidth / thumbnailWidth);
                var thumbnailInterval = metadata.Duration.TotalSeconds / Math.Max(1, thumbnailCount - 1);

                var thumbnailGenerator = new ThumbnailGenerator();
                // Generate at 2x resolution for better quality
                var thumbnails = await Task.Run(() =>
                    thumbnailGenerator.Generate(filePath, TimeSpan.FromSeconds(thumbnailInterval), 200, 112));

                // Use MainWindowViewModel's Timeline instead of creating new instance
                if (_viewModel != null)
                {
                    _viewModel.Timeline.LoadVideo(metadata, thumbnails);

                    // Wire timeline to video player (only once)
                    if (_timelineViewModel == null)
                    {
                        _viewModel.Timeline.CurrentTimeChanged += (sender, newTime) =>
                        {
                            try
                            {
                                if (_frameCache != null)
                                {
                                    // Get and display current frame immediately
                                    // With smart seeking, forward scrubbing is fast without preloading
                                    var frame = _frameCache.GetFrame(newTime);
                                    VideoPlayer.DisplayFrame(frame);

                                    // No preloading - rely on smart seeking + natural LRU cache
                                    // This prevents thread explosion and simplifies the system
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to update video frame for time {Time}", newTime);
                            }
                        };
                    }

                    _timelineViewModel = _viewModel.Timeline;

                    // Timeline DataContext already set in constructor, just show it
                    TimelineControl.IsVisible = true;
                }

                // Display first frame
                var firstFrame = _frameCache.GetFrame(TimeSpan.Zero);
                VideoPlayer.DisplayFrame(firstFrame);
                VideoPlayer.IsVisible = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize video player");
                Title = $"Bref - Error: {ex.Message}";
            }
        }
        catch (NotSupportedException ex)
        {
            Log.Warning(ex, "Unsupported video format");
            Title = $"Bref - Error: Unsupported format";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load video");
            Title = $"Bref - Error: {ex.Message}";
        }
    }

    private async Task<IStorageFile[]?> ShowOpenFileDialogAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select MP4 Video",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("MP4 Videos")
                {
                    Patterns = new[] { "*.mp4", "*.MP4" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);
        return result?.ToArray();
    }

    /// <summary>
    /// Public method to regenerate thumbnails (called by ViewModel)
    /// </summary>
    public async Task RegenerateThumbnailsAsync()
    {
        if (_currentVideoPath == null || _viewModel == null || _viewModel.Timeline.VideoMetadata == null)
            return;

        try
        {
            // Calculate thumbnail count based on window width
            var windowWidth = Width > 0 ? Width : 1200;
            var availableWidth = windowWidth - 40; // Account for margins
            var thumbnailWidth = 100; // Display width
            var thumbnailCount = (int)(availableWidth / thumbnailWidth);

            // Generate at 2x resolution for better quality (200x112 instead of 100x56)
            var genWidth = 200;
            var genHeight = 112;

            // Get virtual duration
            var virtualDuration = _viewModel.VirtualDuration;
            if (virtualDuration.TotalSeconds <= 0)
            {
                Log.Warning("Virtual duration is zero, cannot regenerate thumbnails");
                return;
            }

            var thumbnailInterval = virtualDuration.TotalSeconds / Math.Max(1, thumbnailCount - 1);

            var thumbnails = new List<ThumbnailData>();
            var thumbnailGenerator = new ThumbnailGenerator();

            // Generate thumbnails at virtual timeline positions
            await Task.Run(() =>
            {
                for (int i = 0; i < thumbnailCount; i++)
                {
                    var virtualTime = TimeSpan.FromSeconds(i * thumbnailInterval);

                    // Clamp to virtual duration
                    if (virtualTime > virtualDuration)
                        virtualTime = virtualDuration;

                    // Convert virtual time to source time
                    var sourceTime = _viewModel.Timeline.VirtualToSourceTime(virtualTime);

                    // Generate thumbnail at source time with high resolution
                    var sourceThumbnail = thumbnailGenerator.GenerateSingle(_currentVideoPath, sourceTime, genWidth, genHeight);
                    if (sourceThumbnail != null)
                    {
                        // Create new ThumbnailData with virtual time position for rendering
                        var thumbnail = new ThumbnailData
                        {
                            TimePosition = virtualTime, // Store virtual time for timeline rendering
                            ImageData = sourceThumbnail.ImageData,
                            Width = sourceThumbnail.Width,
                            Height = sourceThumbnail.Height
                        };
                        thumbnails.Add(thumbnail);
                    }
                }
            });

            // Update timeline with new thumbnails
            _viewModel.Timeline.Thumbnails = new ObservableCollection<ThumbnailData>(thumbnails);

            // Update window title with new virtual duration
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to regenerate thumbnails");
        }
    }

    /// <summary>
    /// Update window title with video information and virtual duration
    /// </summary>
    private void UpdateWindowTitle(VideoMetadata? metadata = null)
    {
        if (metadata == null && _viewModel?.Timeline.VideoMetadata != null)
        {
            metadata = _viewModel.Timeline.VideoMetadata;
        }

        if (metadata == null)
        {
            Title = "Bref";
            return;
        }

        var fileName = System.IO.Path.GetFileName(metadata.FilePath);
        var virtualDuration = _viewModel?.VirtualDuration ?? metadata.Duration;

        Title = $"Bref - {fileName} | {metadata.Width}x{metadata.Height} @ {metadata.FrameRate:F0}fps | Duration: {virtualDuration:hh\\:mm\\:ss}";
    }

    /// <summary>
    /// Handle keyboard shortcuts including Space key for Play/Pause toggle
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null)
            return;

        // Space key toggles Play/Pause
        if (e.Key == Key.Space)
        {
            // Toggle between Play and Pause
            if (_viewModel.IsPlaying)
            {
                if (_viewModel.PauseCommand.CanExecute(null))
                {
                    _viewModel.PauseCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else
            {
                if (_viewModel.PlayCommand.CanExecute(null))
                {
                    _viewModel.PlayCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
