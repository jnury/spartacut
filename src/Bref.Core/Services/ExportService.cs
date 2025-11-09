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

    public async Task<bool> ExportAsync(
        ExportOptions options,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!File.Exists(options.SourceFilePath))
        {
            throw new FileNotFoundException($"Source video not found: {options.SourceFilePath}");
        }

        if (options.Segments.KeptSegments.Count == 0)
        {
            throw new InvalidOperationException("Cannot export: no segments to export");
        }

        if (string.IsNullOrWhiteSpace(options.OutputFilePath))
        {
            throw new ArgumentException("Output file path is required", nameof(options));
        }

        // Check if output directory exists
        var outputDir = Path.GetDirectoryName(options.OutputFilePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Log.Information("Created output directory: {Dir}", outputDir);
        }

        // Check if output file already exists
        if (File.Exists(options.OutputFilePath))
        {
            Log.Warning("Output file already exists, will overwrite: {File}", options.OutputFilePath);
        }

        Log.Information("Starting export: {Source} -> {Output}",
            options.SourceFilePath, options.OutputFilePath);

        var startTime = DateTime.Now;

        try
        {
            // Report: Preparing
            progress.Report(new ExportProgress
            {
                Stage = ExportStage.Preparing,
                Percentage = 0,
                Message = "Detecting hardware encoders..."
            });

            // Determine encoder
            var encoder = options.PreferredEncoder;
            if (encoder == null && options.UseHardwareAcceleration)
            {
                encoder = await GetRecommendedEncoderAsync();
            }
            encoder ??= "libx264"; // Software fallback

            Log.Information("Using encoder: {Encoder}", encoder);

            // Build FFmpeg command
            var arguments = BuildFFmpegArguments(options, encoder);
            Log.Debug("FFmpeg arguments: {Args}", arguments);

            // Calculate total frames for progress
            var totalFrames = (long)(options.Segments.TotalDuration.TotalSeconds * options.Metadata.FrameRate);

            progress.Report(new ExportProgress
            {
                Stage = ExportStage.Preparing,
                Percentage = 5,
                Message = $"Building export with {encoder}...",
                TotalFrames = totalFrames
            });

            // Start FFmpeg process
            var success = false;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                // Parse FFmpeg stderr for progress
                var progressInfo = ParseFFmpegProgress(e.Data, totalFrames);
                if (progressInfo.HasValue)
                {
                    var elapsed = DateTime.Now - startTime;
                    TimeSpan? remaining = progressInfo.Value.percentage > 0
                        ? TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - progressInfo.Value.percentage) / progressInfo.Value.percentage)
                        : null;

                    progress.Report(new ExportProgress
                    {
                        Stage = ExportStage.Encoding,
                        Percentage = progressInfo.Value.percentage,
                        Message = $"Encoding frame {progressInfo.Value.frame} of {totalFrames}...",
                        ElapsedTime = elapsed,
                        EstimatedTimeRemaining = remaining,
                        CurrentFrame = progressInfo.Value.frame,
                        TotalFrames = totalFrames
                    });
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            // Wait for completion or cancellation
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Log.Warning("Export cancelled by user");
                    process.Kill();

                    progress.Report(new ExportProgress
                    {
                        Stage = ExportStage.Cancelled,
                        Percentage = 0,
                        Message = "Export cancelled"
                    });

                    return false;
                }

                await Task.Delay(100, CancellationToken.None);
            }

            success = process.ExitCode == 0;

            if (success)
            {
                Log.Information("Export completed successfully in {Elapsed}", DateTime.Now - startTime);

                progress.Report(new ExportProgress
                {
                    Stage = ExportStage.Complete,
                    Percentage = 100,
                    Message = "Export complete!",
                    ElapsedTime = DateTime.Now - startTime
                });
            }
            else
            {
                Log.Error("Export failed with exit code {ExitCode}", process.ExitCode);

                progress.Report(new ExportProgress
                {
                    Stage = ExportStage.Failed,
                    Percentage = 0,
                    Message = $"Export failed (exit code {process.ExitCode})"
                });
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export failed with exception");

            progress.Report(new ExportProgress
            {
                Stage = ExportStage.Failed,
                Percentage = 0,
                Message = $"Export failed: {ex.Message}"
            });

            return false;
        }
    }

    /// <summary>
    /// Parse FFmpeg stderr output for progress information
    /// </summary>
    private (long frame, int percentage)? ParseFFmpegProgress(string line, long totalFrames)
    {
        // FFmpeg progress format: "frame=  123 fps=30 q=28.0 size=    1024kB time=00:00:04.10 ..."
        if (!line.Contains("frame=")) return null;

        try
        {
            var frameMatch = System.Text.RegularExpressions.Regex.Match(line, @"frame=\s*(\d+)");
            if (!frameMatch.Success) return null;

            var frame = long.Parse(frameMatch.Groups[1].Value);
            var percentage = totalFrames > 0 ? (int)(frame * 100 / totalFrames) : 0;

            return (frame, Math.Min(percentage, 99)); // Cap at 99% until complete
        }
        catch
        {
            return null;
        }
    }
}
