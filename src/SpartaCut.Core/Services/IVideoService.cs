using SpartaCut.Core.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SpartaCut.Core.Services;

/// <summary>
/// Service for video file operations (loading, validation, metadata extraction).
/// </summary>
public interface IVideoService
{
    /// <summary>
    /// Loads a video file with metadata and waveform generation.
    /// </summary>
    /// <param name="filePath">Path to video file</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Video metadata</returns>
    /// <exception cref="NotSupportedException">Thrown if video format is not supported</exception>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    /// <exception cref="InvalidDataException">Thrown if file is corrupted</exception>
    Task<VideoMetadata> LoadVideoAsync(
        string filePath,
        IProgress<LoadProgress> progress,
        CancellationToken cancellationToken = default);
}
