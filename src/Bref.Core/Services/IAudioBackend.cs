using System;
using System.Threading.Tasks;

namespace Bref.Core.Services;

/// <summary>
/// Platform-agnostic interface for audio playback backends
/// </summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>
    /// Whether audio is currently loaded
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Current playback position
    /// </summary>
    TimeSpan CurrentTime { get; set; }

    /// <summary>
    /// Total audio duration
    /// </summary>
    TimeSpan TotalTime { get; }

    /// <summary>
    /// Loads audio from WAV file
    /// </summary>
    Task LoadAudioAsync(string audioFilePath);

    /// <summary>
    /// Starts playback
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses playback
    /// </summary>
    void Pause();

    /// <summary>
    /// Stops playback and resets position
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to specific time position
    /// </summary>
    void Seek(TimeSpan position);

    /// <summary>
    /// Sets playback volume (0.0 to 1.0)
    /// </summary>
    void SetVolume(float volume);
}
