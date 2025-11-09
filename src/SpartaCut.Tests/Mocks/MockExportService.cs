using System;
using System.Threading;
using System.Threading.Tasks;
using SpartaCut.Core.Models;
using SpartaCut.Core.Services.Interfaces;

namespace SpartaCut.Tests.Mocks;

/// <summary>
/// Mock implementation of IExportService for testing
/// </summary>
public class MockExportService : IExportService
{
    public Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // Simple mock - just return success
        return Task.FromResult(true);
    }

    public Task<string[]> DetectHardwareEncodersAsync()
    {
        // Return empty array for tests
        return Task.FromResult(Array.Empty<string>());
    }

    public Task<string?> GetRecommendedEncoderAsync()
    {
        // Return null (software encoding) for tests
        return Task.FromResult<string?>(null);
    }
}
