using System;

namespace SpartaCut.Core.Models;

/// <summary>
/// Audio waveform data for timeline visualization.
/// </summary>
public class WaveformData
{
    /// <summary>
    /// Peak samples (min/max pairs) for waveform rendering.
    /// Each sample represents a time window.
    /// </summary>
    public required float[] Peaks { get; init; }

    /// <summary>
    /// Duration of audio.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Sample rate used for peak extraction.
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Number of samples per peak (resolution).
    /// </summary>
    public required int SamplesPerPeak { get; init; }
}
