using FFmpeg.AutoGen;
using Bref.Models;
using Serilog;
using System;
using System.IO;

namespace Bref.FFmpeg;

/// <summary>
/// Extracts metadata and frames from video files using FFmpeg.
/// </summary>
public unsafe class FrameExtractor : IDisposable
{
    private AVFormatContext* _formatContext = null;
    private AVCodecContext* _codecContext = null;
    private int _videoStreamIndex = -1;
    private bool _isDisposed = false;

    /// <summary>
    /// Extract metadata from a video file without opening codec.
    /// </summary>
    /// <param name="filePath">Path to video file.</param>
    /// <returns>Video metadata.</returns>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidOperationException">Failed to open or parse video file.</exception>
    public VideoMetadata ExtractMetadata(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Video file not found: {filePath}");
        }

        Log.Information("Extracting metadata from: {FilePath}", filePath);

        AVFormatContext* formatContext = null;
        try
        {
            // Open video file
            var result = ffmpeg.avformat_open_input(&formatContext, filePath, null, null);
            if (result < 0)
            {
                throw new InvalidOperationException($"Failed to open video file. FFmpeg error code: {result}");
            }

            // Read stream information
            result = ffmpeg.avformat_find_stream_info(formatContext, null);
            if (result < 0)
            {
                throw new InvalidOperationException($"Failed to find stream info. FFmpeg error code: {result}");
            }

            // Find video stream
            var videoStreamIndex = -1;
            for (int i = 0; i < formatContext->nb_streams; i++)
            {
                if (formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStreamIndex = i;
                    break;
                }
            }

            if (videoStreamIndex == -1)
            {
                throw new InvalidOperationException("No video stream found in file");
            }

            var videoStream = formatContext->streams[videoStreamIndex];
            var codecParams = videoStream->codecpar;

            // Calculate duration
            var durationSeconds = formatContext->duration / (double)ffmpeg.AV_TIME_BASE;
            var duration = TimeSpan.FromSeconds(durationSeconds);

            // Calculate frame rate
            var frameRate = (double)videoStream->r_frame_rate.num / videoStream->r_frame_rate.den;

            // Get codec name
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
            var codecName = codec != null ? ffmpeg.avcodec_get_name(codecParams->codec_id) : "unknown";

            // Get pixel format
            var pixelFormat = ffmpeg.av_get_pix_fmt_name((AVPixelFormat)codecParams->format);

            // Get file size
            var fileInfo = new FileInfo(filePath);

            var metadata = new VideoMetadata
            {
                FilePath = filePath,
                Duration = duration,
                Width = codecParams->width,
                Height = codecParams->height,
                FrameRate = frameRate,
                CodecName = codecName,
                PixelFormat = pixelFormat,
                Bitrate = codecParams->bit_rate,
                FileSizeBytes = fileInfo.Length
            };

            Log.Information("Metadata extracted: {Metadata}", metadata);

            return metadata;
        }
        finally
        {
            // Clean up
            if (formatContext != null)
            {
                ffmpeg.avformat_close_input(&formatContext);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        if (_codecContext != null)
        {
            fixed (AVCodecContext** codecContextPtr = &_codecContext)
            {
                ffmpeg.avcodec_free_context(codecContextPtr);
            }
        }

        if (_formatContext != null)
        {
            fixed (AVFormatContext** formatContextPtr = &_formatContext)
            {
                ffmpeg.avformat_close_input(formatContextPtr);
            }
        }

        _isDisposed = true;
    }
}
