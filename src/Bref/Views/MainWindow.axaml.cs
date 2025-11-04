using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Bref.Controls;
using Bref.FFmpeg;
using Bref.Models;
using Bref.Services;
using Bref.ViewModels;
using Serilog;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bref.Views;

public partial class MainWindow : Window
{
    private FrameCache? _frameCache;
    private TimelineViewModel? _timelineViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize FFmpeg on window load
        try
        {
            FFmpegSetup.Initialize();
            Log.Information("FFmpeg initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize FFmpeg");
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = $"ERROR: Failed to initialize FFmpeg - {ex.Message}";
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

            // Update window title with video info
            var fileName = System.IO.Path.GetFileName(metadata.FilePath);
            Title = $"Bref - {fileName}";

            // Update status line
            StatusTextBlock.Text = $"Loaded: {fileName} | {metadata.Width}x{metadata.Height} @ {metadata.FrameRate:F0}fps | {metadata.Duration:hh\\:mm\\:ss}";

            Log.Information("Successfully loaded video: {Metadata}", metadata);

            // Generate thumbnails for timeline
            try
            {
                StatusTextBlock.Text = "Generating thumbnails...";

                var thumbnailGenerator = new ThumbnailGenerator();
                var thumbnails = await Task.Run(() =>
                    thumbnailGenerator.Generate(filePath, TimeSpan.FromSeconds(5), 160, 90));

                // Create timeline viewmodel
                var timelineViewModel = new TimelineViewModel();
                timelineViewModel.LoadVideo(metadata, thumbnails);

                // Wire timeline to video player
                timelineViewModel.CurrentTimeChanged += (sender, newTime) =>
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

                _timelineViewModel = timelineViewModel;

                // Set timeline datacontext and show
                TimelineControl.DataContext = timelineViewModel;
                TimelineControl.IsVisible = true;

                Log.Information("Timeline populated with {Count} thumbnails", thumbnails.Count);

                // Initialize frame cache
                StatusTextBlock.Text = "Initializing frame cache...";

                _frameCache?.Dispose();
                _frameCache = new FrameCache(filePath, capacity: 60);

                // Display first frame
                var firstFrame = _frameCache.GetFrame(TimeSpan.Zero);
                VideoPlayer.DisplayFrame(firstFrame);
                VideoPlayer.IsVisible = true;

                // Update final status
                StatusTextBlock.Text = $"{fileName} | {metadata.Width}x{metadata.Height} @ {metadata.FrameRate:F0}fps | {metadata.Duration:hh\\:mm\\:ss} | Ready";

                Log.Information("Frame cache initialized and first frame displayed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize video player");
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }
        catch (NotSupportedException ex)
        {
            Log.Warning(ex, "Unsupported video format");
            StatusTextBlock.Text = $"Error: Unsupported format - {ex.Message}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load video");
            StatusTextBlock.Text = $"Error: {ex.Message}";
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
}
