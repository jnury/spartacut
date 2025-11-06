using System;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Handles audio playback synchronized with video frames
/// STUB IMPLEMENTATION - Actual audio playback will be implemented in Week 8
/// </summary>
public class AudioPlayer : IDisposable
{
    private float _volume = 0f;
    private bool _disposed = false;

    /// <summary>
    /// Current volume (0.0 to 1.0)
    /// </summary>
    public float Volume => _volume;

    /// <summary>
    /// Loads audio from video file
    /// </summary>
    public void LoadAudio(string videoFilePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        try
        {
            // Extract audio to temp file (NAudio can't read MP4 directly)
            // For now, we'll skip audio extraction and implement later
            Log.Information("AudioPlayer: Load audio not yet implemented");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load audio from {FilePath}", videoFilePath);
            throw;
        }
    }

    /// <summary>
    /// Sets playback volume (0.0 to 1.0)
    /// </summary>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        // Actual volume control will be implemented in Week 8
    }

    /// <summary>
    /// Seeks to specific time position
    /// </summary>
    public void Seek(TimeSpan position)
    {
        // Stub - actual seeking will be implemented in Week 8
    }

    /// <summary>
    /// Starts playback
    /// </summary>
    public void Play()
    {
        // Stub - actual playback will be implemented in Week 8
    }

    /// <summary>
    /// Pauses playback
    /// </summary>
    public void Pause()
    {
        // Stub - actual pause will be implemented in Week 8
    }

    /// <summary>
    /// Stops playback
    /// </summary>
    public void Stop()
    {
        // Stub - actual stop will be implemented in Week 8
    }

    public void Dispose()
    {
        if (_disposed) return;

        // No resources to dispose in stub implementation
        _disposed = true;
    }
}
