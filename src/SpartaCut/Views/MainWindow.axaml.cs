using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SpartaCut.Controls;
using SpartaCut.Core.FFmpeg;
using SpartaCut.Core.Models;
using SpartaCut.Core.Services;
using SpartaCut.Core.ViewModels;
using SpartaCut.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SpartaCut.Views;

public partial class MainWindow : Window
{
    private TimelineViewModel? _timelineViewModel;
    private MainWindowViewModel? _viewModel;
    private VlcPlayerControl? _vlcPlayer;
    private string? _currentVideoPath;
    private double _lastRegenerationWidth = 0;
    private bool _isVideoLoaded = false;

    // Menu item references for state management
    private NativeMenuItem? _nativeSaveAsMenuItem;
    private NativeMenuItem? _nativeUndoMenuItem;
    private NativeMenuItem? _nativeRedoMenuItem;
    private MenuItem? _windowsSaveAsMenuItem;
    private MenuItem? _windowsUndoMenuItem;
    private MenuItem? _windowsRedoMenuItem;

    /// <summary>
    /// Public accessor for ViewModel (for menu handlers in App.axaml.cs)
    /// </summary>
    public MainWindowViewModel? ViewModel => _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Get reference to VLC player control
        _vlcPlayer = this.FindControl<VlcPlayerControl>("VlcPlayer");

        // Subscribe to window size changes for thumbnail regeneration
        this.PropertyChanged += OnWindowPropertyChanged;

        // Subscribe to key events for Space key handling
        this.KeyDown += OnWindowKeyDown;

        // Initialize ViewModel with dependency injection
        var playbackEngine = new VlcPlaybackEngine();
        var exportService = new ExportService();
        _viewModel = new MainWindowViewModel(playbackEngine, exportService);
        DataContext = _viewModel;

        // Set thumbnail regeneration callback
        _viewModel.RegenerateThumbnailsCallback = RegenerateThumbnailsAsync;

        // Set save file dialog callback
        _viewModel.ShowSaveFileDialogCallback = ShowSaveFileDialogAsync;

        // Subscribe to ViewModel property changes for menu state updates
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

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

        // Wire up Windows menu item click handlers and store references
        WireUpMenuHandlers();

        // Get references to native menu items for state management
        StoreNativeMenuReferences();

        // Hide in-window menu on macOS (use native menu bar instead)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var windowsMenu = this.FindControl<Menu>("WindowsMenu");
            if (windowsMenu != null)
            {
                windowsMenu.IsVisible = false;
            }
        }

        // Initialize menu item states (all disabled until video loaded)
        UpdateMenuStates();

        // FFMpegCore automatically locates ffmpeg binary - no initialization needed
        Log.Information("MainWindow initialized");
    }

    private void WireUpMenuHandlers()
    {
        var menuItemOpen = this.FindControl<MenuItem>("MenuItemOpen");
        _windowsSaveAsMenuItem = this.FindControl<MenuItem>("MenuItemSaveAs");
        var menuItemExit = this.FindControl<MenuItem>("MenuItemExit");
        _windowsUndoMenuItem = this.FindControl<MenuItem>("MenuItemUndo");
        _windowsRedoMenuItem = this.FindControl<MenuItem>("MenuItemRedo");
        var menuItemAbout = this.FindControl<MenuItem>("MenuItemAbout");

        if (menuItemOpen != null)
            menuItemOpen.Click += (s, e) => OpenVideoMenuItem_Click(s, EventArgs.Empty);

        if (_windowsSaveAsMenuItem != null)
            _windowsSaveAsMenuItem.Click += (s, e) => SaveAsMenuItem_Click(s, EventArgs.Empty);

        if (menuItemExit != null)
            menuItemExit.Click += (s, e) => QuitMenuItem_Click(s, EventArgs.Empty);

        if (_windowsUndoMenuItem != null)
            _windowsUndoMenuItem.Click += (s, e) => UndoMenuItem_Click(s, EventArgs.Empty);

        if (_windowsRedoMenuItem != null)
            _windowsRedoMenuItem.Click += (s, e) => RedoMenuItem_Click(s, EventArgs.Empty);

        if (menuItemAbout != null)
            menuItemAbout.Click += (s, e) => AboutMenuItem_Click(s, EventArgs.Empty);
    }

    private void StoreNativeMenuReferences()
    {
        // Get native menu items from MainWindow's NativeMenu
        if (NativeMenu.GetMenu(this) is NativeMenu nativeMenu)
        {
            foreach (var item in nativeMenu.Items)
            {
                if (item is NativeMenuItem menuItem)
                {
                    if (menuItem.Header?.ToString() == "File" && menuItem.Menu != null)
                    {
                        foreach (var subItem in menuItem.Menu.Items)
                        {
                            if (subItem is NativeMenuItem sub && sub.Header?.ToString() == "Export...")
                            {
                                _nativeSaveAsMenuItem = sub;
                                break;
                            }
                        }
                    }
                    else if (menuItem.Header?.ToString() == "Edit" && menuItem.Menu != null)
                    {
                        foreach (var subItem in menuItem.Menu.Items)
                        {
                            if (subItem is NativeMenuItem sub)
                            {
                                if (sub.Header?.ToString() == "Undo")
                                    _nativeUndoMenuItem = sub;
                                else if (sub.Header?.ToString() == "Redo")
                                    _nativeRedoMenuItem = sub;
                            }
                        }
                    }
                }
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.UndoEnabled) ||
            e.PropertyName == nameof(MainWindowViewModel.RedoEnabled))
        {
            UpdateMenuStates();
        }
    }

    private void UpdateMenuStates()
    {
        var undoEnabled = _viewModel?.UndoEnabled ?? false;
        var redoEnabled = _viewModel?.RedoEnabled ?? false;
        var videoLoaded = _isVideoLoaded;

        // Update native menu items (macOS)
        if (_nativeSaveAsMenuItem != null)
            _nativeSaveAsMenuItem.IsEnabled = videoLoaded;
        if (_nativeUndoMenuItem != null)
            _nativeUndoMenuItem.IsEnabled = undoEnabled;
        if (_nativeRedoMenuItem != null)
            _nativeRedoMenuItem.IsEnabled = redoEnabled;

        // Update Windows menu items
        if (_windowsSaveAsMenuItem != null)
            _windowsSaveAsMenuItem.IsEnabled = videoLoaded;
        if (_windowsUndoMenuItem != null)
            _windowsUndoMenuItem.IsEnabled = undoEnabled;
        if (_windowsRedoMenuItem != null)
            _windowsRedoMenuItem.IsEnabled = redoEnabled;
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

        // Unsubscribe from events to prevent memory leaks
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        }

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

            // Initialize ViewModel with video metadata
            if (_viewModel != null)
            {
                _viewModel.InitializeVideo(metadata);

                // Bind VLC MediaPlayer to control
                if (_vlcPlayer != null && _viewModel.PlaybackEngine is VlcPlaybackEngine vlcEngine && vlcEngine.MediaPlayer != null)
                {
                    _vlcPlayer.SetMediaPlayer(vlcEngine.MediaPlayer);
                }
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
                    _timelineViewModel = _viewModel.Timeline;

                    // Subscribe to delete requested event from timeline
                    _timelineViewModel.DeleteRequested += OnTimelineDeleteRequested;

                    // Timeline DataContext already set in constructor, just show it
                    TimelineControl.IsVisible = true;

                    // Mark video as loaded and enable menu items
                    _isVideoLoaded = true;
                    UpdateMenuStates();
                }

                // VLC handles video display automatically
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize video player");
                Title = $"Sparta Cut - Error: {ex.Message}";
            }
        }
        catch (NotSupportedException ex)
        {
            Log.Warning(ex, "Unsupported video format");
            Title = $"Sparta Cut - Error: Unsupported format";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load video");
            Title = $"Sparta Cut - Error: {ex.Message}";
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

    private async Task<string?> ShowSaveFileDialogAsync()
    {
        var options = new FilePickerSaveOptions
        {
            Title = "Export Video",
            DefaultExtension = "mp4",
            SuggestedFileName = "exported-video.mp4",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("MP4 Videos")
                {
                    Patterns = new[] { "*.mp4" }
                }
            }
        };

        var result = await StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
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
            Title = "Sparta Cut";
            return;
        }

        var fileName = System.IO.Path.GetFileName(metadata.FilePath);
        var virtualDuration = _viewModel?.VirtualDuration ?? metadata.Duration;

        Title = $"Sparta Cut - {fileName} | {metadata.Width}x{metadata.Height} @ {metadata.FrameRate:F0}fps | Duration: {virtualDuration:hh\\:mm\\:ss}";
    }

    /// <summary>
    /// Handle keyboard shortcuts including Space key for Play/Pause toggle
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null)
            return;

        // Check for Ctrl/Cmd modifier
        var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Ctrl+Z for Undo
        if (isCtrlPressed && !isShiftPressed && e.Key == Key.Z)
        {
            if (_viewModel.UndoCommand.CanExecute(null))
            {
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
            }
        }
        // Ctrl+Shift+Z or Ctrl+Y for Redo
        else if (isCtrlPressed && ((isShiftPressed && e.Key == Key.Z) || e.Key == Key.Y))
        {
            if (_viewModel.RedoCommand.CanExecute(null))
            {
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
            }
        }
        // Space key toggles Play/Pause
        else if (e.Key == Key.Space)
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
        // M key toggles Mute/Unmute
        else if (e.Key == Key.M)
        {
            _viewModel.ToggleMuteCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Public method to show About dialog (called from App.axaml.cs for macOS menu)
    /// </summary>
    public void ShowAboutDialogPublic()
    {
        ShowAboutDialog();
    }

    /// <summary>
    /// Menu item handler for About dialog
    /// </summary>
    private void AboutMenuItem_Click(object? sender, EventArgs e)
    {
        ShowAboutDialog();
    }

    /// <summary>
    /// Show About dialog
    /// </summary>
    private async void ShowAboutDialog()
    {
        var aboutDialog = new Window
        {
            Title = "About Sparta Cut",
            Width = 520,
            Height = 650,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"))
        };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        // App title
        content.Children.Add(new TextBlock
        {
            Text = "Sparta Cut",
            FontSize = 32,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        // Version
        content.Children.Add(new TextBlock
        {
            Text = "Version 0.12.0",
            FontSize = 14,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        content.Children.Add(new Border
        {
            Height = 1,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333")),
            Margin = new Thickness(0, 10, 0, 10)
        });

        // Description
        content.Children.Add(new TextBlock
        {
            Text = "Fast video editor for removing unwanted segments",
            FontSize = 12,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center
        });

        // GitHub link
        var githubLink = CreateHyperlink("View on GitHub", "https://github.com/jnury/spartacut");
        content.Children.Add(githubLink);

        // Third-Party Licenses
        content.Children.Add(new TextBlock
        {
            Text = "Third-Party Software Licenses",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            Margin = new Thickness(0, 15, 0, 5)
        });

        content.Children.Add(new TextBlock
        {
            Text = "Sparta Cut uses the following open-source components:",
            FontSize = 10,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // LibVLC
        AddDependency(content, "LibVLC", "LGPL-2.1+", "VideoLAN",
            "Video playback framework. Dynamically linked.",
            "https://code.videolan.org/videolan/vlc");

        // LibVLCSharp
        AddDependency(content, "LibVLCSharp", "LGPL-2.1+", "VideoLAN",
            "Cross-platform .NET wrapper for LibVLC.",
            "https://code.videolan.org/videolan/LibVLCSharp");

        // Avalonia UI
        AddDependency(content, "Avalonia UI", "MIT", "The Avalonia Project",
            "Cross-platform XAML-based UI framework.",
            "https://github.com/AvaloniaUI/Avalonia");

        // FFMpegCore
        AddDependency(content, "FFMpegCore", "MIT", "Vlad Jerca and contributors",
            ".NET wrapper for FFmpeg.",
            "https://github.com/rosenbjerg/FFMpegCore");

        // NAudio
        AddDependency(content, "NAudio", "MIT", "Mark Heath and contributors",
            "Audio library for .NET (waveform generation).",
            "https://github.com/naudio/NAudio");

        // CommunityToolkit.Mvvm
        AddDependency(content, "CommunityToolkit.Mvvm", "MIT", ".NET Foundation",
            "MVVM toolkit for modern .NET applications.",
            "https://github.com/CommunityToolkit/dotnet");

        // Serilog
        AddDependency(content, "Serilog", "Apache-2.0", "Serilog Contributors",
            "Diagnostic logging library for .NET.",
            "https://github.com/serilog/serilog");

        // SkiaSharp
        AddDependency(content, "SkiaSharp", "MIT", "Microsoft Corporation",
            "Cross-platform 2D graphics API.",
            "https://github.com/mono/SkiaSharp");

        // License files
        content.Children.Add(new TextBlock
        {
            Text = "License Information",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            Margin = new Thickness(0, 15, 0, 5)
        });

        var licensesLink = CreateHyperlink("View Full Third-Party Licenses", "https://github.com/jnury/spartacut/blob/main/LICENSE-THIRD-PARTY.txt");
        content.Children.Add(licensesLink);

        var lgplLink = CreateHyperlink("LGPL-2.1 License", "https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html");
        content.Children.Add(lgplLink);

        content.Children.Add(new Border
        {
            Height = 1,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#333333")),
            Margin = new Thickness(0, 15, 0, 10)
        });

        // Copyright
        content.Children.Add(new TextBlock
        {
            Text = $"© {DateTime.Now.Year} Sparta Cut\nBuilt with Avalonia UI + C# + LibVLC",
            FontSize = 10,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            LineHeight = 14
        });

        // Close button
        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Padding = new Thickness(30, 8),
            Margin = new Thickness(0, 15, 0, 0)
        };
        closeButton.Click += (sender, args) => aboutDialog.Close();
        content.Children.Add(closeButton);

        scrollViewer.Content = content;
        aboutDialog.Content = scrollViewer;
        await aboutDialog.ShowDialog(this);
    }

    private void AddDependency(StackPanel parent, string name, string license, string copyright, string description, string sourceUrl)
    {
        // Dependency name and license
        var nameText = new TextBlock
        {
            Text = $"{name} ({license})",
            FontSize = 10,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
            Margin = new Thickness(0, 6, 0, 2)
        };
        parent.Children.Add(nameText);

        // Copyright and description
        var descText = new TextBlock
        {
            Text = $"© {copyright}\n{description}",
            FontSize = 9,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#999999")),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            LineHeight = 12
        };
        parent.Children.Add(descText);

        // Source code link
        var link = CreateHyperlink("Source Code", sourceUrl, 9);
        link.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        link.Margin = new Thickness(0, 2, 0, 0);
        parent.Children.Add(link);
    }

    private StackPanel CreateHyperlink(string text, string url, int fontSize = 11)
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 5
        };

        var button = new Button
        {
            Content = text,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#007ACC")),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            FontSize = fontSize
        };

        button.Click += (sender, args) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open URL: {Url}", url);
            }
        };

        // Add underline on hover effect
        button.PointerEntered += (s, e) =>
        {
            if (button.Content is string content)
            {
                var textBlock = new TextBlock
                {
                    Text = content,
                    TextDecorations = Avalonia.Media.TextDecorations.Underline
                };
                button.Content = textBlock;
            }
        };

        button.PointerExited += (s, e) =>
        {
            if (button.Content is TextBlock textBlock)
            {
                button.Content = textBlock.Text;
            }
        };

        panel.Children.Add(button);
        return panel;
    }

    /// <summary>
    /// Menu item handler for Undo
    /// </summary>
    private void UndoMenuItem_Click(object? sender, EventArgs e)
    {
        if (_viewModel?.UndoCommand.CanExecute(null) == true)
        {
            _viewModel.UndoCommand.Execute(null);
        }
    }

    /// <summary>
    /// Menu item handler for Redo
    /// </summary>
    private void RedoMenuItem_Click(object? sender, EventArgs e)
    {
        if (_viewModel?.RedoCommand.CanExecute(null) == true)
        {
            _viewModel.RedoCommand.Execute(null);
        }
    }

    /// <summary>
    /// Menu item handler for Export...
    /// </summary>
    private void SaveAsMenuItem_Click(object? sender, EventArgs e)
    {
        if (_viewModel?.ExportCommand.CanExecute(null) == true)
        {
            _viewModel.ExportCommand.Execute(null);
        }
    }

    /// <summary>
    /// Menu item handler for Quit/Exit
    /// </summary>
    private void QuitMenuItem_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnTimelineDeleteRequested(object? sender, EventArgs e)
    {
        // Handle delete request from timeline icon
        if (_viewModel?.DeleteSelectionCommand.CanExecute(null) == true)
        {
            _viewModel.DeleteSelectionCommand.Execute(null);
        }
    }

    /// <summary>
    /// Propagate mouse movement from video player area to overlay for fade behavior
    /// </summary>
    private void OnPlayerPointerMoved(object? sender, PointerEventArgs e)
    {
        // Get PlayerControls reference if needed
        var playerControls = this.FindControl<PlayerControlsOverlay>("PlayerControls");
        if (playerControls != null)
        {
            // Call method directly to avoid event bubbling loop
            playerControls.OnPointerMoved(sender, e);
        }
    }
}
