using Avalonia;
using System;
using System.Reflection;
using SpartaCut.Utilities;
using Serilog;

namespace SpartaCut;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Initialize Serilog first
            LoggerSetup.Initialize();

            // Log version information
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            Log.Information("Bref {Version} is running", version);
            Log.Information("Starting Avalonia application");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            LoggerSetup.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
