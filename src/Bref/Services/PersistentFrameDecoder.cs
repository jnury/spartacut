using System;
using System.IO;
using Bref.FFmpeg;
using Bref.Models;
using FFmpeg.AutoGen;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Persistent frame decoder that keeps video file open for fast sequential decoding.
/// Always decodes frames to 640×360 resolution for efficient timeline scrubbing.
/// NOT thread-safe - caller must synchronize access.
/// </summary>
public unsafe class PersistentFrameDecoder : IDisposable
{
    private readonly string _videoFilePath;
    private const int TargetWidth = 640;
    private const int TargetHeight = 360;

    private AVFormatContext* _formatContext;
    private AVCodecContext* _codecContext;
    private SwsContext* _swsContext;
    private int _videoStreamIndex;
    private double _timeBase;
    private bool _isDisposed;

    // Track current decoder position to avoid unnecessary seeks
    private double _currentPositionSeconds = -1.0;

    /// <summary>
    /// Opens video file and initializes decoder contexts.
    /// Throws if file cannot be opened or decoded.
    /// </summary>
    public PersistentFrameDecoder(string videoFilePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoFilePath);
        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);

        _videoFilePath = videoFilePath;

        try
        {
            InitializeDecoder();
        }
        catch
        {
            // Cleanup on initialization failure
            Dispose();
            throw;
        }
    }

    private void InitializeDecoder()
    {
        FFmpegSetup.Initialize();

        // Open video file
        AVFormatContext* formatCtx = null;
        if (ffmpeg.avformat_open_input(&formatCtx, _videoFilePath, null, null) != 0)
            throw new InvalidDataException("Failed to open video file");

        _formatContext = formatCtx;

        if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
            throw new InvalidDataException("Failed to find stream information");

        // Find video stream
        _videoStreamIndex = -1;
        for (int i = 0; i < _formatContext->nb_streams; i++)
        {
            if (_formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoStreamIndex = i;
                break;
            }
        }

        if (_videoStreamIndex == -1)
            throw new InvalidDataException("No video stream found");

        var stream = _formatContext->streams[_videoStreamIndex];
        var codecParams = stream->codecpar;
        _timeBase = ffmpeg.av_q2d(stream->time_base);

        // Find and open codec
        var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
        if (codec == null)
            throw new InvalidDataException("Codec not found");

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        if (_codecContext == null)
            throw new OutOfMemoryException("Failed to allocate codec context");

        ffmpeg.avcodec_parameters_to_context(_codecContext, codecParams);

        if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
            throw new InvalidDataException("Failed to open codec");

        // Create persistent scaling context for 640×360
        _swsContext = ffmpeg.sws_getContext(
            _codecContext->width, _codecContext->height, _codecContext->pix_fmt,
            TargetWidth, TargetHeight, AVPixelFormat.AV_PIX_FMT_RGB24,
            ffmpeg.SWS_BILINEAR, null, null, null);

        if (_swsContext == null)
            throw new InvalidOperationException("Failed to create scaling context");

        Log.Information("PersistentFrameDecoder initialized for {FilePath} (scaling {SourceWidth}×{SourceHeight} → {TargetWidth}×{TargetHeight})",
            _videoFilePath, _codecContext->width, _codecContext->height, TargetWidth, TargetHeight);
    }

    /// <summary>
    /// Decodes frame at specified timestamp, always returning 640×360 resolution.
    /// Smart seeking: only seeks if going backward or jumping far forward.
    /// </summary>
    public VideoFrame DecodeFrameAt(TimeSpan timePosition)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (timePosition < TimeSpan.Zero)
            throw new ArgumentException("Time position cannot be negative", nameof(timePosition));

        var targetSeconds = timePosition.TotalSeconds;
        var timeDelta = targetSeconds - _currentPositionSeconds;

        // Only seek if:
        // 1. Going backward (timeDelta < 0)
        // 2. Jumping far forward (> 2 seconds)
        // 3. First frame (_currentPositionSeconds < 0)
        bool needsSeek = _currentPositionSeconds < 0 || timeDelta < 0 || timeDelta > 2.0;

        if (needsSeek)
        {
            // Seek to target time
            long timestamp = (long)(targetSeconds / _timeBase);
            if (ffmpeg.av_seek_frame(_formatContext, _videoStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            {
                Log.Warning("Seek failed for {Time}, returning closest available frame", timePosition);
            }

            ffmpeg.avcodec_flush_buffers(_codecContext);
            _currentPositionSeconds = -1.0; // Will be updated by DecodeClosestFrame
        }

        // Decode frame at position (will decode forward from current position if no seek)
        var frame = DecodeClosestFrame(timePosition);

        if (frame == null)
            throw new InvalidDataException($"Failed to decode frame at {timePosition}");

        // Update current position
        _currentPositionSeconds = frame.TimePosition.TotalSeconds;

        return frame;
    }

    private VideoFrame? DecodeClosestFrame(TimeSpan targetTime)
    {
        var packet = ffmpeg.av_packet_alloc();
        var frame = ffmpeg.av_frame_alloc();
        var targetSeconds = targetTime.TotalSeconds;

        VideoFrame? closestFrame = null;
        double closestDistance = double.MaxValue;

        try
        {
            while (ffmpeg.av_read_frame(_formatContext, packet) >= 0)
            {
                if (packet->stream_index == _videoStreamIndex)
                {
                    if (ffmpeg.avcodec_send_packet(_codecContext, packet) == 0)
                    {
                        while (ffmpeg.avcodec_receive_frame(_codecContext, frame) == 0)
                        {
                            // Get actual frame timestamp
                            var pts = frame->best_effort_timestamp;
                            if (pts == ffmpeg.AV_NOPTS_VALUE)
                                pts = frame->pts;

                            var frameSeconds = pts * _timeBase;
                            var distance = Math.Abs(frameSeconds - targetSeconds);

                            // If this frame is closer to target, keep it
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                var actualTime = TimeSpan.FromSeconds(frameSeconds);
                                closestFrame = ConvertFrameToRGB24(frame, actualTime);
                            }

                            // If we've passed the target, we have the closest frame
                            if (frameSeconds >= targetSeconds)
                            {
                                ffmpeg.av_packet_unref(packet);
                                return closestFrame;
                            }
                        }
                    }
                }

                ffmpeg.av_packet_unref(packet);
            }

            return closestFrame;
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.av_frame_free(&frame);
        }
    }

    private VideoFrame ConvertFrameToRGB24(AVFrame* frame, TimeSpan timePosition)
    {
        // Create target frame for 640×360 RGB24
        var scaledFrame = ffmpeg.av_frame_alloc();
        try
        {
            scaledFrame->width = TargetWidth;
            scaledFrame->height = TargetHeight;
            scaledFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;

            ffmpeg.av_frame_get_buffer(scaledFrame, 32);

            // Scale from source to 640×360 RGB24
            ffmpeg.sws_scale(_swsContext, frame->data, frame->linesize, 0, _codecContext->height,
                scaledFrame->data, scaledFrame->linesize);

            // Copy to managed byte array
            var imageData = new byte[TargetWidth * TargetHeight * 3]; // RGB24
            var srcPtr = (byte*)scaledFrame->data[0];
            var linesize = scaledFrame->linesize[0];

            fixed (byte* dstPtr = imageData)
            {
                for (int y = 0; y < TargetHeight; y++)
                {
                    Buffer.MemoryCopy(
                        srcPtr + (y * linesize),
                        dstPtr + (y * TargetWidth * 3),
                        TargetWidth * 3,
                        TargetWidth * 3);
                }
            }

            return new VideoFrame
            {
                TimePosition = timePosition,
                ImageData = imageData,
                Width = TargetWidth,
                Height = TargetHeight
            };
        }
        finally
        {
            ffmpeg.av_frame_free(&scaledFrame);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        // Mark as disposed FIRST to prevent new decode operations
        _isDisposed = true;

        // Note: Caller (FrameCache) must hold _decodeLock to ensure
        // no decode operations are in progress during disposal

        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        if (_codecContext != null)
        {
            var ctx = _codecContext;
            ffmpeg.avcodec_free_context(&ctx);
            _codecContext = null;
        }

        if (_formatContext != null)
        {
            var ctx = _formatContext;
            ffmpeg.avformat_close_input(&ctx);
            _formatContext = null;
        }

        Log.Information("PersistentFrameDecoder disposed for {FilePath}", _videoFilePath);
    }
}
