using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Runtime.InteropServices;

namespace Bref;

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

            // Setup macOS menu bar
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupMacOSMenu(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupMacOSMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        // Application menu (will be named "Bref" automatically from App.axaml Name property)
        var appMenu = new NativeMenuItem();
        var appSubMenu = new NativeMenu();

        var aboutItem = new NativeMenuItem("About Bref");
        aboutItem.Click += (sender, args) => ShowAboutDialog(desktop.MainWindow);
        appSubMenu.Add(aboutItem);

        appSubMenu.Add(new NativeMenuItemSeparator());

        var quitItem = new NativeMenuItem("Quit Bref") { Gesture = KeyGesture.Parse("Cmd+Q") };
        quitItem.Click += (sender, args) => desktop.Shutdown();
        appSubMenu.Add(quitItem);

        appMenu.Menu = appSubMenu;
        menu.Add(appMenu);

        // File menu
        var fileMenu = new NativeMenuItem("File");
        var fileSubMenu = new NativeMenu();

        var openItem = new NativeMenuItem("Open Video...") { Gesture = KeyGesture.Parse("Cmd+O") };
        openItem.Click += (sender, args) =>
        {
            if (desktop.MainWindow is Views.MainWindow mainWindow)
            {
                // Trigger the load button click programmatically
                mainWindow.TriggerLoadVideo();
            }
        };
        fileSubMenu.Add(openItem);

        fileMenu.Menu = fileSubMenu;
        menu.Add(fileMenu);

        if (desktop.MainWindow != null)
        {
            NativeMenu.SetMenu(desktop.MainWindow, menu);
        }
    }

    private async void ShowAboutDialog(Window? owner)
    {
        if (owner == null) return;

        var aboutDialog = new Window
        {
            Title = "About Bref",
            Width = 400,
            Height = 300,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"))
        };

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15
        };

        content.Children.Add(new TextBlock
        {
            Text = "Bref",
            FontSize = 32,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "Version 0.1.0 - Week 1 POC",
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

        content.Children.Add(new TextBlock
        {
            Text = "Fast, focused video editor for removing unwanted segments",
            FontSize = 12,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = "Technology Stack:",
            FontSize = 12,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            Margin = new Thickness(0, 10, 0, 5)
        });

        content.Children.Add(new TextBlock
        {
            Text = "• Avalonia UI 11.3.6\n• .NET 8\n• FFmpeg 7.1.2\n• Serilog 3.1.1",
            FontSize = 11,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC")),
            LineHeight = 18
        });

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Padding = new Thickness(30, 5),
            Margin = new Thickness(0, 15, 0, 0)
        };
        closeButton.Click += (sender, args) => aboutDialog.Close();
        content.Children.Add(closeButton);

        aboutDialog.Content = content;
        await aboutDialog.ShowDialog(owner);
    }
}
