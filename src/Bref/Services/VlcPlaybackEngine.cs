using System;
using System.IO;
using LibVLCSharp.Shared;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Manages video playback using LibVLC
/// </summary>
public class VlcPlaybackEngine : IDisposable
{
    private LibVLC? _libVLC;
    private bool _disposed = false;

    static VlcPlaybackEngine()
    {
        // Static constructor: Set VLC_PLUGIN_PATH before any instance is created
        // This ensures the environment variable is set before LibVLC initialization
        if (OperatingSystem.IsMacOS())
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var pluginPath = Path.Combine(baseDir, "plugins");
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
            Log.Information("Static initialization: Set VLC_PLUGIN_PATH to: {PluginPath}", pluginPath);
        }
    }

    public VlcPlaybackEngine()
    {
        try
        {
            // Initialize LibVLCSharp Core
            Core.Initialize();
            Log.Information("LibVLC Core initialized");

            // Create LibVLC instance
            _libVLC = new LibVLC();
            Log.Information("LibVLC instance created successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize LibVLC. Exception: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            if (ex.InnerException != null)
            {
                Log.Error("Inner exception: {InnerExceptionType}, Message: {InnerMessage}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }
            throw new InvalidOperationException(
                "Video playback unavailable. LibVLC could not initialize.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _libVLC?.Dispose();
        _disposed = true;
    }
}
