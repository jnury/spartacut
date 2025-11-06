using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Windows-specific audio backend using NAudio's WaveOutEvent
/// </summary>
public class WindowsAudioBackend : IAudioBackend
{
    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioFileReader;
    private float _volume = 1.0f;
    private bool _disposed = false;

    public bool IsLoaded => _audioFileReader != null;

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

    public TimeSpan TotalTime => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

    public async Task LoadAudioAsync(string audioFilePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioBackend));

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

            Log.Information("Windows audio backend loaded: {FilePath}, Duration={Duration}",
                audioFilePath, _audioFileReader.TotalTime);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load audio in Windows backend: {FilePath}", audioFilePath);
            DisposeAudio();
            throw;
        }
    }

    public void Play()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioBackend));

        if (_waveOut == null || _audioFileReader == null)
        {
            Log.Warning("Cannot play: No audio loaded (Windows backend)");
            return;
        }

        if (_waveOut.PlaybackState != PlaybackState.Playing)
        {
            _waveOut.Play();
            Log.Debug("Windows audio playback started at {Time}", CurrentTime);
        }
    }

    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioBackend));

        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
            Log.Debug("Windows audio playback paused at {Time}", CurrentTime);
        }
    }

    public void Stop()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioBackend));

        if (_waveOut != null)
        {
            _waveOut.Stop();
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.Zero;
            }
            Log.Debug("Windows audio playback stopped");
        }
    }

    public void Seek(TimeSpan position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WindowsAudioBackend));

        if (_audioFileReader != null)
        {
            var clampedPosition = TimeSpan.FromSeconds(
                Math.Clamp(position.TotalSeconds, 0, _audioFileReader.TotalTime.TotalSeconds));
            _audioFileReader.CurrentTime = clampedPosition;
            Log.Debug("Windows audio seeked to {Time}", clampedPosition);
        }
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        if (_audioFileReader != null)
        {
            _audioFileReader.Volume = _volume;
        }
    }

    private void DisposeAudio()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisposeAudio();
        _disposed = true;

        Log.Debug("WindowsAudioBackend disposed");
    }
}
