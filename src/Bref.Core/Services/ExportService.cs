using Bref.Core.Models;
using Bref.Core.Services.Interfaces;
using FFMpegCore;
using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bref.Core.Services;

/// <summary>
/// Service for exporting edited videos using FFmpeg
/// </summary>
public class ExportService : IExportService
{
    private static readonly string[] HardwareEncoders = new[]
    {
        "h264_nvenc",       // NVIDIA
        "h264_qsv",         // Intel Quick Sync
        "h264_amf",         // AMD
    };

    public async Task<string[]> DetectHardwareEncodersAsync()
    {
        Log.Information("Detecting available hardware encoders...");
        var availableEncoders = new System.Collections.Generic.List<string>();

        foreach (var encoder in HardwareEncoders)
        {
            if (await IsEncoderAvailableAsync(encoder))
            {
                availableEncoders.Add(encoder);
                Log.Information("Hardware encoder available: {Encoder}", encoder);
            }
        }

        if (availableEncoders.Count == 0)
        {
            Log.Information("No hardware encoders available, will use software encoding (libx264)");
        }

        return availableEncoders.ToArray();
    }

    public async Task<string?> GetRecommendedEncoderAsync()
    {
        var available = await DetectHardwareEncodersAsync();

        // Priority: NVENC > Quick Sync > AMF
        if (available.Contains("h264_nvenc"))
            return "h264_nvenc";
        if (available.Contains("h264_qsv"))
            return "h264_qsv";
        if (available.Contains("h264_amf"))
            return "h264_amf";

        return null; // Software fallback
    }

    private async Task<bool> IsEncoderAvailableAsync(string encoderName)
    {
        try
        {
            // Run ffmpeg -encoders and check if encoder is listed
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = "-encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Check if encoder appears in output
            return output.Contains(encoderName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check encoder availability: {Encoder}", encoderName);
            return false;
        }
    }

    public Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement in next task
        throw new NotImplementedException("Export implementation coming in Task 3");
    }
}
