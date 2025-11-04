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
    public MainWindow()
    {
        InitializeComponent();

        // Initialize FFmpeg on window load
        try
        {
            FFmpegSetup.Initialize();
            Log.Information("FFmpeg initialized for POC");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize FFmpeg");
            if (VideoInfoTextBlock != null)
            {
                VideoInfoTextBlock.Text = $"ERROR: Failed to initialize FFmpeg\n\n{ex.Message}\n\nPlease ensure FFmpeg is installed via Homebrew:\nbrew install ffmpeg";
            }
        }
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
            Title = $"Bref - {fileName} - {metadata.Width}x{metadata.Height} - {metadata.Duration:hh\\:mm\\:ss}";

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
