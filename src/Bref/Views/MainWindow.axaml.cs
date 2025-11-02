using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Bref.FFmpeg;
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

            // Extract metadata
            VideoInfoTextBlock.Text = "Loading video metadata...";

            using var extractor = new FrameExtractor();
            var metadata = extractor.ExtractMetadata(filePath);

            // Check if supported
            var supportedStatus = metadata.IsSupported()
                ? "✓ SUPPORTED (H.264)"
                : "✗ NOT SUPPORTED (MVP requires H.264)";

            // Build info display
            var info = new StringBuilder();
            info.AppendLine("=== VIDEO METADATA ===");
            info.AppendLine();
            info.AppendLine($"File: {metadata.FilePath}");
            info.AppendLine($"Status: {supportedStatus}");
            info.AppendLine();
            info.AppendLine($"Duration: {metadata.Duration:hh\\:mm\\:ss\\.fff}");
            info.AppendLine($"Resolution: {metadata.Width} x {metadata.Height}");
            info.AppendLine($"Frame Rate: {metadata.FrameRate:F2} fps");
            info.AppendLine($"Codec: {metadata.CodecName}");
            info.AppendLine($"Pixel Format: {metadata.PixelFormat}");
            info.AppendLine($"Bitrate: {metadata.Bitrate / 1000:N0} kbps");
            info.AppendLine($"File Size: {metadata.GetFileSizeFormatted()}");
            info.AppendLine();
            info.AppendLine("=== FFmpeg VERSION ===");
            info.AppendLine();
            info.AppendLine(FFmpegSetup.GetFFmpegVersion());
            info.AppendLine();
            info.AppendLine("=== POC SUCCESS ===");
            info.AppendLine();
            info.AppendLine("✓ Avalonia UI working");
            info.AppendLine("✓ FFmpeg.AutoGen integrated");
            info.AppendLine("✓ Video metadata extraction working");
            info.AppendLine("✓ File picker functional");
            info.AppendLine();
            info.AppendLine("Week 1 POC: COMPLETE");

            VideoInfoTextBlock.Text = info.ToString();

            Log.Information("Successfully loaded video metadata: {Metadata}", metadata);
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
