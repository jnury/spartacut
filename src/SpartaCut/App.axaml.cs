using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;

namespace SpartaCut;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void AboutMenuItem_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShowAboutDialog(desktop.MainWindow);
        }
    }

    private async void ShowAboutDialog(Window? owner)
    {
        if (owner == null) return;

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
        await aboutDialog.ShowDialog(owner);
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
                Console.WriteLine($"Failed to open URL: {ex.Message}");
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
}
