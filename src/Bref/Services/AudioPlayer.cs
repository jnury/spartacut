using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Handles audio playback synchronized with video frames using platform-specific backends
/// </summary>
public class AudioPlayer : IDisposable
{
    private IAudioBackend? _backend;
    private float _volume = 1.0f;
    private bool _disposed = false;
    private string? _currentFilePath;
    private string? _tempAudioFilePath;

    public AudioPlayer()
    {
        // Create platform-specific backend
        _backend = CreateAudioBackend();
        Log.Information("AudioPlayer initialized with {BackendType} backend", _backend.GetType().Name);
    }

    /// <summary>
    /// Current volume (0.0 to 1.0)
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            _backend?.SetVolume(_volume);
        }
    }

    /// <summary>
    /// Whether audio file is loaded
    /// </summary>
    public bool IsLoaded => _backend?.IsLoaded ?? false;

    /// <summary>
    /// Current playback position
    /// </summary>
    public TimeSpan CurrentTime
    {
        get => _backend?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_backend != null)
            {
                _backend.CurrentTime = value;
            }
        }
    }

    /// <summary>
    /// Total audio duration
    /// </summary>
    public TimeSpan TotalTime => _backend?.TotalTime ?? TimeSpan.Zero;

    /// <summary>
    /// Creates the appropriate audio backend for the current platform
    /// </summary>
    private static IAudioBackend CreateAudioBackend()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Log.Information("Detected Windows platform - using NAudio backend");
            return new WindowsAudioBackend();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Log.Information("Detected macOS platform - using OpenAL backend");
            return new OpenALAudioBackend();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Log.Information("Detected Linux platform - using OpenAL backend");
            return new OpenALAudioBackend();
        }
        else
        {
            Log.Warning("Unknown platform - defaulting to OpenAL backend");
            return new OpenALAudioBackend();
        }
    }

    /// <summary>
    /// Loads audio from WAV file
    /// </summary>
    public async Task LoadAudioAsync(string audioFilePath, bool isTempFile = false)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException($"Audio file not found: {audioFilePath}", audioFilePath);
        }

        if (_backend == null)
        {
            throw new InvalidOperationException("Audio backend not initialized");
        }

        try
        {
            // Dispose existing audio
            DisposeAudio();

            // Load new audio file via backend
            await _backend.LoadAudioAsync(audioFilePath);

            // Set volume on backend
            _backend.SetVolume(_volume);

            _currentFilePath = audioFilePath;
            _tempAudioFilePath = isTempFile ? audioFilePath : null;

            Log.Information("Loaded audio from {FilePath}, Duration={Duration}",
                audioFilePath, TotalTime);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load audio from {FilePath}", audioFilePath);
            DisposeAudio();
            throw;
        }
    }

    /// <summary>
    /// Sets playback volume (0.0 to 1.0)
    /// </summary>
    public void SetVolume(float volume)
    {
        Volume = volume;
    }

    /// <summary>
    /// Starts playback
    /// </summary>
    public void Play()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        _backend?.Play();
    }

    /// <summary>
    /// Pauses playback
    /// </summary>
    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        _backend?.Pause();
    }

    /// <summary>
    /// Stops playback and resets position
    /// </summary>
    public void Stop()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        _backend?.Stop();
    }

    /// <summary>
    /// Seeks to specific time position
    /// </summary>
    public void Seek(TimeSpan position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));
        _backend?.Seek(position);
    }

    private void DisposeAudio()
    {
        // Delete temp audio file
        if (_tempAudioFilePath != null && File.Exists(_tempAudioFilePath))
        {
            try
            {
                File.Delete(_tempAudioFilePath);
                Log.Debug("Deleted temp audio file: {Path}", _tempAudioFilePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete temp audio file: {Path}", _tempAudioFilePath);
            }
            _tempAudioFilePath = null;
        }

        _currentFilePath = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisposeAudio();
        _backend?.Dispose();
        _backend = null;
        _disposed = true;

        Log.Debug("AudioPlayer disposed");
    }
}
