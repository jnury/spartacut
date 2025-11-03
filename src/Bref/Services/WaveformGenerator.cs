using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bref.Models;
using FFMpegCore;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Generates audio waveform data from video files using FFMpegCore.
/// Cross-platform implementation for macOS and Windows.
/// </summary>
public class WaveformGenerator
{
    private const int TargetPeakCount = 3000; // ~3000 peaks for typical timeline width

    /// <summary>
    /// Generates waveform data from video file.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="progress">Progress reporter (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Waveform data</returns>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    public WaveformData Generate(
        string videoFilePath,
        IProgress<int> progress,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        Log.Information("Generating waveform for: {FilePath}", videoFilePath);

        try
        {
            // Step 1: Extract audio to temporary WAV file (0-50%)
            progress.Report(10);
            var tempWavPath = Path.Combine(Path.GetTempPath(), $"bref_audio_{Guid.NewGuid()}.wav");

            try
            {
                // Use FFMpegCore to extract audio to 16-bit PCM WAV
                Log.Debug("Extracting audio to temporary WAV: {TempPath}", tempWavPath);

                var videoInfo = FFProbe.Analyse(videoFilePath);
                var duration = videoInfo.Duration;

                progress.Report(20);
                cancellationToken.ThrowIfCancellationRequested();

                // Extract audio to WAV format (16-bit PCM, mono for simplicity)
                FFMpegArguments
                    .FromFileInput(videoFilePath)
                    .OutputToFile(tempWavPath, overwrite: true, options => options
                        .WithAudioCodec("pcm_s16le")
                        .WithAudioSamplingRate(44100)
                        .WithCustomArgument("-ac 1")) // Convert to mono
                    .ProcessSynchronously();

                progress.Report(50);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Read WAV file and extract peaks (50-100%)
                Log.Debug("Reading WAV file and extracting peaks");
                var waveformData = ExtractPeaksFromWav(tempWavPath, duration, progress, cancellationToken);

                Log.Information("Waveform generated: {PeakCount} peaks, Duration: {Duration}",
                    waveformData.Peaks.Length, waveformData.Duration);

                progress.Report(100);
                return waveformData;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempWavPath))
                {
                    try
                    {
                        File.Delete(tempWavPath);
                        Log.Debug("Deleted temporary WAV file: {TempPath}", tempWavPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete temporary WAV file: {TempPath}", tempWavPath);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            Log.Error(ex, "Failed to generate waveform for: {FilePath}", videoFilePath);
            throw new InvalidDataException($"Failed to generate waveform: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extracts peak samples from a WAV file.
    /// </summary>
    private WaveformData ExtractPeaksFromWav(
        string wavFilePath,
        TimeSpan duration,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        using var fileStream = File.OpenRead(wavFilePath);
        using var reader = new BinaryReader(fileStream);

        // Parse WAV header (simple implementation for PCM WAV)
        var riff = new string(reader.ReadChars(4));
        if (riff != "RIFF")
            throw new InvalidDataException("Invalid WAV file: Missing RIFF header");

        reader.ReadInt32(); // File size
        var wave = new string(reader.ReadChars(4));
        if (wave != "WAVE")
            throw new InvalidDataException("Invalid WAV file: Missing WAVE header");

        // Find 'fmt ' chunk
        while (true)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                reader.ReadInt16(); // Audio format (1 = PCM)
                var channels = reader.ReadInt16();
                var sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // Byte rate
                reader.ReadInt16(); // Block align
                var bitsPerSample = reader.ReadInt16();

                // Skip remaining fmt chunk
                if (chunkSize > 16)
                    reader.ReadBytes(chunkSize - 16);

                // Find 'data' chunk
                while (true)
                {
                    var dataChunkId = new string(reader.ReadChars(4));
                    var dataChunkSize = reader.ReadInt32();

                    if (dataChunkId == "data")
                    {
                        // Extract peaks from audio data
                        return ExtractPeaksFromAudioData(
                            reader,
                            dataChunkSize,
                            sampleRate,
                            bitsPerSample,
                            duration,
                            progress,
                            cancellationToken);
                    }
                    else
                    {
                        // Skip unknown chunk
                        reader.ReadBytes(dataChunkSize);
                    }
                }
            }
            else
            {
                // Skip unknown chunk
                reader.ReadBytes(chunkSize);
            }
        }
    }

    /// <summary>
    /// Extracts peak samples from PCM audio data.
    /// </summary>
    private WaveformData ExtractPeaksFromAudioData(
        BinaryReader reader,
        int dataSize,
        int sampleRate,
        int bitsPerSample,
        TimeSpan duration,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        var bytesPerSample = bitsPerSample / 8;
        var totalSamples = dataSize / bytesPerSample;

        // Calculate samples per peak to achieve target peak count
        var samplesPerPeak = Math.Max(1, totalSamples / TargetPeakCount);

        var peaks = new List<float>();
        var buffer = new byte[samplesPerPeak * bytesPerSample];

        long totalBytesRead = 0;
        int lastProgress = 50;

        while (totalBytesRead < dataSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesToRead = (int)Math.Min(buffer.Length, dataSize - totalBytesRead);
            var bytesRead = reader.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0) break;

            totalBytesRead += bytesRead;

            // Calculate min/max for this chunk
            float min = 0f, max = 0f;
            for (int i = 0; i < bytesRead; i += bytesPerSample)
            {
                // Convert bytes to normalized float sample (-1.0 to 1.0)
                float sample = bitsPerSample switch
                {
                    16 => BitConverter.ToInt16(buffer, i) / 32768f,
                    32 => BitConverter.ToInt32(buffer, i) / 2147483648f,
                    _ => 0f
                };

                if (sample < min) min = sample;
                if (sample > max) max = sample;
            }

            // Store min and max as separate peaks for rendering
            peaks.Add(min);
            peaks.Add(max);

            // Report progress (map from 50% to 100%)
            var currentProgress = 50 + (int)(totalBytesRead * 50 / dataSize);
            if (currentProgress != lastProgress)
            {
                progress.Report(currentProgress);
                lastProgress = currentProgress;
            }
        }

        return new WaveformData
        {
            Peaks = peaks.ToArray(),
            Duration = duration,
            SampleRate = sampleRate,
            SamplesPerPeak = samplesPerPeak
        };
    }
}
