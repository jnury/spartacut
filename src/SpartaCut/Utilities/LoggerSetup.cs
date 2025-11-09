using Serilog;
using System;
using System.IO;

namespace SpartaCut.Utilities;

/// <summary>
/// Configures Serilog logger for the application.
/// Logs to both console (Debug) and file (all environments).
/// </summary>
public static class LoggerSetup
{
    /// <summary>
    /// Initialize Serilog with console and file sinks.
    /// Log file location: ~/Library/Application Support/SpartaCut/logs/spartacut-{Date}.log (macOS)
    /// </summary>
    public static void Initialize()
    {
        var logDirectory = GetLogDirectory();
        var logFilePath = Path.Combine(logDirectory, "spartacut-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Sparta Cut application starting");
        Log.Information("Log directory: {LogDirectory}", logDirectory);
    }

    /// <summary>
    /// Get platform-specific log directory.
    /// macOS: ~/Library/Application Support/SpartaCut/logs
    /// Windows: %APPDATA%\SpartaCut\logs
    /// Linux: ~/.local/share/SpartaCut/logs
    /// </summary>
    private static string GetLogDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDirectory = Path.Combine(appDataPath, "Sparta Cut", "logs");

        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
            // Don't log here - logger isn't initialized yet
        }

        return logDirectory;
    }

    /// <summary>
    /// Flush and close the logger (call on application shutdown).
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("Sparta Cut application shutting down");
        Log.CloseAndFlush();
    }
}
