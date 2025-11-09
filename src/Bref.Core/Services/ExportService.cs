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

    /// <summary>
    /// Build FFmpeg filter_complex for segment concatenation
    /// </summary>
    private string BuildFilterComplex(SegmentList segments, VideoMetadata metadata)
    {
        var keptSegments = segments.KeptSegments;

        if (keptSegments.Count == 0)
        {
            throw new InvalidOperationException("No segments to export");
        }

        if (keptSegments.Count == 1)
        {
            // Single segment - simple trim filter
            var seg = keptSegments[0];
            var start = seg.SourceStart.TotalSeconds;
            var duration = seg.Duration.TotalSeconds;

            return $"[0:v]trim=start={start}:duration={duration},setpts=PTS-STARTPTS[v]; " +
                   $"[0:a]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS[a]";
        }

        // Multiple segments - concat filter
        var filterParts = new System.Collections.Generic.List<string>();
        var videoLabels = new System.Collections.Generic.List<string>();
        var audioLabels = new System.Collections.Generic.List<string>();

        for (int i = 0; i < keptSegments.Count; i++)
        {
            var seg = keptSegments[i];
            var start = seg.SourceStart.TotalSeconds;
            var duration = seg.Duration.TotalSeconds;

            var vLabel = $"v{i}";
            var aLabel = $"a{i}";

            // Trim and reset PTS for each segment
            filterParts.Add($"[0:v]trim=start={start}:duration={duration},setpts=PTS-STARTPTS[{vLabel}]");
            filterParts.Add($"[0:a]atrim=start={start}:duration={duration},asetpts=PTS-STARTPTS[{aLabel}]");

            videoLabels.Add($"[{vLabel}]");
            audioLabels.Add($"[{aLabel}]");
        }

        // Concatenate all segments
        var videoConcat = string.Join("", videoLabels) + $"concat=n={keptSegments.Count}:v=1:a=0[v]";
        var audioConcat = string.Join("", audioLabels) + $"concat=n={keptSegments.Count}:v=0:a=1[a]";

        filterParts.Add(videoConcat);
        filterParts.Add(audioConcat);

        return string.Join("; ", filterParts);
    }

    /// <summary>
    /// Build complete FFmpeg arguments for export
    /// </summary>
    private string BuildFFmpegArguments(ExportOptions options, string encoder)
    {
        var filterComplex = BuildFilterComplex(options.Segments, options.Metadata);

        var args = new System.Text.StringBuilder();

        // Input file
        args.Append($"-i \"{options.SourceFilePath}\" ");

        // Filter complex for segment concatenation
        args.Append($"-filter_complex \"{filterComplex}\" ");

        // Map filtered video and audio
        args.Append("-map \"[v]\" -map \"[a]\" ");

        // Encoder settings
        if (encoder == "libx264")
        {
            // Software encoding
            args.Append("-c:v libx264 -preset medium -crf 23 ");
        }
        else
        {
            // Hardware encoding
            args.Append($"-c:v {encoder} ");

            // Encoder-specific settings
            if (encoder == "h264_nvenc")
            {
                args.Append("-preset p4 -rc vbr -cq 23 ");
            }
            else if (encoder == "h264_qsv")
            {
                args.Append("-preset medium -global_quality 23 ");
            }
            else if (encoder == "h264_amf")
            {
                args.Append("-quality balanced -rc cqp -qp 23 ");
            }
        }

        // Audio encoding (copy if same codec, re-encode if needed)
        args.Append("-c:a aac -b:a 192k ");

        // Output format
        args.Append("-movflags +faststart "); // Enable streaming
        args.Append($"\"{options.OutputFilePath}\"");

        return args.ToString();
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
