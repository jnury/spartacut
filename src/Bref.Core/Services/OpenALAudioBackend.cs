using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Bref.Core.Services;

/// <summary>
/// Cross-platform audio backend using OpenAL (for macOS and other platforms)
/// </summary>
public class OpenALAudioBackend : IAudioBackend
{
    private ALDevice _device;
    private ALContext _context;
    private int _source;
    private int _buffer;
    private bool _isLoaded = false;
    private bool _disposed = false;
    private float _volume = 1.0f;

    // Audio file metadata
    private int _sampleRate;
    private int _channels;
    private int _bitsPerSample;
    private int _dataSize;
    private TimeSpan _totalTime;

    // Playback state tracking
    private Timer? _positionUpdateTimer;
    private double _currentTimeSeconds = 0;

    public bool IsLoaded => _isLoaded;

    public TimeSpan CurrentTime
    {
        get
        {
            if (!_isLoaded) return TimeSpan.Zero;

            // Get actual playback position from OpenAL
            AL.GetSource(_source, ALGetSourcei.SampleOffset, out int sampleOffset);
            if (AL.GetError() == ALError.NoError && _sampleRate > 0)
            {
                _currentTimeSeconds = (double)sampleOffset / _sampleRate;
            }

            return TimeSpan.FromSeconds(_currentTimeSeconds);
        }
        set
        {
            if (!_isLoaded) return;

            _currentTimeSeconds = Math.Clamp(value.TotalSeconds, 0, _totalTime.TotalSeconds);
            int sampleOffset = (int)(_currentTimeSeconds * _sampleRate);
            AL.Source(_source, ALSourcei.SampleOffset, sampleOffset);
            CheckALError("SetCurrentTime");
        }
    }

    public TimeSpan TotalTime => _totalTime;

    public OpenALAudioBackend()
    {
        try
        {
            // Initialize OpenAL device and context
            _device = ALC.OpenDevice(null); // null = default device
            if (_device == ALDevice.Null)
            {
                throw new InvalidOperationException("Failed to open OpenAL device");
            }

            _context = ALC.CreateContext(_device, (int[])null!);
            if (_context == ALContext.Null)
            {
                ALC.CloseDevice(_device);
                throw new InvalidOperationException("Failed to create OpenAL context");
            }

            ALC.MakeContextCurrent(_context);
            CheckALCError(_device, "MakeContextCurrent");

            Log.Information("OpenAL audio backend initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize OpenAL audio backend");
            throw;
        }
    }

    public async Task LoadAudioAsync(string audioFilePath)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenALAudioBackend));

        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException($"Audio file not found: {audioFilePath}", audioFilePath);
        }

        try
        {
            // Dispose existing audio
            DisposeAudio();

            // Load WAV file
            var audioData = LoadWavFile(audioFilePath);

            // Create OpenAL buffer and source
            _buffer = AL.GenBuffer();
            CheckALError("GenBuffer");

            _source = AL.GenSource();
            CheckALError("GenSource");

            // Determine OpenAL format
            ALFormat format = GetALFormat(_channels, _bitsPerSample);

            // Upload audio data to buffer
            AL.BufferData(_buffer, format, audioData, _sampleRate);
            CheckALError("BufferData");

            // Attach buffer to source
            AL.Source(_source, ALSourcei.Buffer, _buffer);
            CheckALError("Source Buffer");

            // Set initial volume
            AL.Source(_source, ALSourcef.Gain, _volume);
            CheckALError("Source Gain");

            // Calculate total duration
            _totalTime = TimeSpan.FromSeconds((double)_dataSize / (_sampleRate * _channels * (_bitsPerSample / 8)));
            _currentTimeSeconds = 0;
            _isLoaded = true;

            Log.Information("OpenAL audio backend loaded: {FilePath}, Duration={Duration}, SampleRate={SampleRate}, Channels={Channels}",
                audioFilePath, _totalTime, _sampleRate, _channels);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load audio in OpenAL backend: {FilePath}", audioFilePath);
            DisposeAudio();
            throw;
        }
    }

    public void Play()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenALAudioBackend));

        if (!_isLoaded)
        {
            Log.Warning("Cannot play: No audio loaded (OpenAL backend)");
            return;
        }

        AL.GetSource(_source, ALGetSourcei.SourceState, out int state);
        if ((ALSourceState)state != ALSourceState.Playing)
        {
            AL.SourcePlay(_source);
            CheckALError("SourcePlay");
            Log.Debug("OpenAL audio playback started at {Time}", CurrentTime);
        }
    }

    public void Pause()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenALAudioBackend));

        if (!_isLoaded) return;

        AL.GetSource(_source, ALGetSourcei.SourceState, out int state);
        if ((ALSourceState)state == ALSourceState.Playing)
        {
            AL.SourcePause(_source);
            CheckALError("SourcePause");
            Log.Debug("OpenAL audio playback paused at {Time}", CurrentTime);
        }
    }

    public void Stop()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenALAudioBackend));

        if (!_isLoaded) return;

        AL.SourceStop(_source);
        CheckALError("SourceStop");
        _currentTimeSeconds = 0;
        AL.Source(_source, ALSourcei.SampleOffset, 0);
        CheckALError("SourceStop Reset");
        Log.Debug("OpenAL audio playback stopped");
    }

    public void Seek(TimeSpan position)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenALAudioBackend));

        if (!_isLoaded) return;

        CurrentTime = position;
        Log.Debug("OpenAL audio seeked to {Time}", position);
    }

    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        if (_isLoaded)
        {
            AL.Source(_source, ALSourcef.Gain, _volume);
            CheckALError("SetVolume");
        }
    }

    private byte[] LoadWavFile(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        // Read RIFF header
        string signature = new string(reader.ReadChars(4));
        if (signature != "RIFF")
        {
            throw new InvalidDataException($"Invalid WAV file: Missing RIFF signature");
        }

        reader.ReadInt32(); // File size - 8
        string format = new string(reader.ReadChars(4));
        if (format != "WAVE")
        {
            throw new InvalidDataException($"Invalid WAV file: Missing WAVE format");
        }

        // Read fmt chunk
        string fmtSignature = new string(reader.ReadChars(4));
        if (fmtSignature != "fmt ")
        {
            throw new InvalidDataException($"Invalid WAV file: Missing fmt chunk");
        }

        int fmtChunkSize = reader.ReadInt32();
        reader.ReadInt16(); // Audio format (1 = PCM)
        _channels = reader.ReadInt16();
        _sampleRate = reader.ReadInt32();
        reader.ReadInt32(); // Byte rate
        reader.ReadInt16(); // Block align
        _bitsPerSample = reader.ReadInt16();

        // Skip any extra fmt bytes
        int extraFmtBytes = fmtChunkSize - 16;
        if (extraFmtBytes > 0)
        {
            reader.ReadBytes(extraFmtBytes);
        }

        // Find data chunk (skip other chunks)
        while (fileStream.Position < fileStream.Length)
        {
            string chunkSignature = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkSignature == "data")
            {
                _dataSize = chunkSize;
                return reader.ReadBytes(chunkSize);
            }
            else
            {
                // Skip unknown chunk
                fileStream.Seek(chunkSize, SeekOrigin.Current);
            }
        }

        throw new InvalidDataException($"Invalid WAV file: Missing data chunk");
    }

    private ALFormat GetALFormat(int channels, int bitsPerSample)
    {
        return (channels, bitsPerSample) switch
        {
            (1, 8) => ALFormat.Mono8,
            (1, 16) => ALFormat.Mono16,
            (2, 8) => ALFormat.Stereo8,
            (2, 16) => ALFormat.Stereo16,
            _ => throw new NotSupportedException($"Unsupported audio format: {channels} channels, {bitsPerSample} bits per sample")
        };
    }

    private void CheckALError(string operation)
    {
        ALError error = AL.GetError();
        if (error != ALError.NoError)
        {
            Log.Error("OpenAL error during {Operation}: {Error}", operation, error);
        }
    }

    private void CheckALCError(ALDevice device, string operation)
    {
        AlcError error = ALC.GetError(device);
        if (error != AlcError.NoError)
        {
            Log.Error("OpenAL context error during {Operation}: {Error}", operation, error);
        }
    }

    private void DisposeAudio()
    {
        _positionUpdateTimer?.Dispose();
        _positionUpdateTimer = null;

        if (_isLoaded)
        {
            if (_source != 0)
            {
                AL.SourceStop(_source);
                AL.Source(_source, ALSourcei.Buffer, 0); // Detach buffer
                AL.DeleteSource(_source);
                _source = 0;
            }

            if (_buffer != 0)
            {
                AL.DeleteBuffer(_buffer);
                _buffer = 0;
            }

            _isLoaded = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisposeAudio();

        if (_context != ALContext.Null)
        {
            ALC.MakeContextCurrent(ALContext.Null);
            ALC.DestroyContext(_context);
            _context = ALContext.Null;
        }

        if (_device != ALDevice.Null)
        {
            ALC.CloseDevice(_device);
            _device = ALDevice.Null;
        }

        _disposed = true;
        Log.Debug("OpenALAudioBackend disposed");
    }
}
