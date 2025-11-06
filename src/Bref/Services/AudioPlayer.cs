using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Handles audio playback synchronized with video frames using NAudio
/// </summary>
public class AudioPlayer : IDisposable
{
    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioFileReader;
    private float _volume = 1.0f;
    private bool _disposed = false;
    private string? _currentFilePath;

    /// <summary>
    /// Current volume (0.0 to 1.0)
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_audioFileReader != null)
            {
                _audioFileReader.Volume = _volume;
            }
        }
    }

    /// <summary>
    /// Whether audio file is loaded
    /// </summary>
    public bool IsLoaded => _audioFileReader != null;

    /// <summary>
    /// Current playback position
    /// </summary>
    public TimeSpan CurrentTime
    {
        get => _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = value;
            }
        }
    }

    /// <summary>
    /// Total audio duration
    /// </summary>
    public TimeSpan TotalTime => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

    /// <summary>
    /// Loads audio from WAV file
    /// </summary>
    public async Task LoadAudioAsync(string audioFilePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException($"Audio file not found: {audioFilePath}", audioFilePath);
        }

        try
        {
            // Dispose existing audio
            DisposeAudio();

            // Load new audio file
            _audioFileReader = new AudioFileReader(audioFilePath);
            _audioFileReader.Volume = _volume;

            // Initialize wave output
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioFileReader);

            _currentFilePath = audioFilePath;

            Log.Information("Loaded audio from {FilePath}, Duration={Duration}",
                audioFilePath, _audioFileReader.TotalTime);

            await Task.CompletedTask; // Keep async signature for future enhancements
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

        if (_waveOut == null || _audioFileReader == null)
        {
            Log.Warning("Cannot play: No audio loaded");
            return;
        }

        if (_waveOut.PlaybackState != PlaybackState.Playing)
        {
            _waveOut.Play();
            Log.Debug("Audio playback started at {Time}", CurrentTime);
        }
    }

    /// <summary>
    /// Pauses playback
    /// </summary>
    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            Log.Debug("Audio playback paused at {Time}", CurrentTime);
        }
    }

    /// <summary>
    /// Stops playback and resets position
    /// </summary>
    public void Stop()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        if (_waveOut != null)
        {
            _waveOut.Stop();
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.Zero;
            }
            Log.Debug("Audio playback stopped");
        }
    }

    /// <summary>
    /// Seeks to specific time position
    /// </summary>
    public void Seek(TimeSpan position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioPlayer));

        if (_audioFileReader != null)
        {
            var clampedPosition = TimeSpan.FromSeconds(
                Math.Clamp(position.TotalSeconds, 0, _audioFileReader.TotalTime.TotalSeconds));
            _audioFileReader.CurrentTime = clampedPosition;
            Log.Debug("Audio seeked to {Time}", clampedPosition);
        }
    }

    private void DisposeAudio()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;

        _currentFilePath = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisposeAudio();
        _disposed = true;

        Log.Debug("AudioPlayer disposed");
    }
}
