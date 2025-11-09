using System;
using System.Threading;
using System.Threading.Tasks;
using SpartaCut.Core.Models;

namespace SpartaCut.Core.Services.Interfaces;

/// <summary>
/// Service for exporting edited videos
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export video with segment filtering
    /// </summary>
    /// <param name="options">Export options</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if export succeeded</returns>
    Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect available hardware encoders
    /// </summary>
    /// <returns>List of available encoder names</returns>
    Task<string[]> DetectHardwareEncodersAsync();

    /// <summary>
    /// Get recommended encoder for current system
    /// </summary>
    /// <returns>Encoder name or null if only software available</returns>
    Task<string?> GetRecommendedEncoderAsync();
}
