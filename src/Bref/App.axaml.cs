using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Bref.FFmpeg;
using Serilog;
using System;

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
            // Test FFmpeg initialization
            try
            {
                FFmpegSetup.Initialize();
                Log.Information("FFmpeg version: {Version}", FFmpegSetup.GetFFmpegVersion());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize FFmpeg");
            }

            desktop.MainWindow = new Views.MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}