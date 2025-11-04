using System;
using System.IO;
using Bref.FFmpeg;
using Bref.Models;
using FFmpeg.AutoGen;
using Serilog;
using SkiaSharp;

namespace Bref.Services;

/// <summary>
/// Decodes individual video frames at specific timestamps using FFmpeg.
/// NOT thread-safe - use one instance per thread or synchronize access.
/// </summary>
public unsafe class FrameDecoder : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Decodes a single frame at the specified timestamp.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="timePosition">Time position to extract frame</param>
    /// <returns>Decoded video frame</returns>
    /// <exception cref="FileNotFoundException">Video file not found</exception>
    /// <exception cref="ArgumentException">Invalid time position</exception>
    /// <exception cref="InvalidDataException">Failed to decode frame</exception>
    public VideoFrame DecodeFrame(string videoFilePath, TimeSpan timePosition)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (timePosition < TimeSpan.Zero)
            throw new ArgumentException("Time position cannot be negative", nameof(timePosition));

        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);

        Log.Debug("Decoding frame at {Time} from {FilePath}", timePosition, videoFilePath);

        AVFormatContext* formatContext = null;
        AVCodecContext* codecContext = null;

        try
        {
            // Initialize FFmpeg
            FFmpegSetup.Initialize();

            // Open video file
            if (ffmpeg.avformat_open_input(&formatContext, videoFilePath, null, null) != 0)
                throw new InvalidDataException("Failed to open video file");

            if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
                throw new InvalidDataException("Failed to find stream information");

            // Find video stream
            int videoStreamIndex = -1;
            for (int i = 0; i < formatContext->nb_streams; i++)
            {
                if (formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    videoStreamIndex = i;
                    break;
                }
            }

            if (videoStreamIndex == -1)
                throw new InvalidDataException("No video stream found");

            var stream = formatContext->streams[videoStreamIndex];
            var codecParams = stream->codecpar;

            // Find and open codec
            var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
            if (codec == null)
                throw new InvalidDataException("Codec not found");

            codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (codecContext == null)
                throw new OutOfMemoryException("Failed to allocate codec context");

            ffmpeg.avcodec_parameters_to_context(codecContext, codecParams);

            if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                throw new InvalidDataException("Failed to open codec");

            // Seek to target time
            long timestamp = (long)(timePosition.TotalSeconds / ffmpeg.av_q2d(stream->time_base));
            if (ffmpeg.av_seek_frame(formatContext, videoStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            {
                Log.Warning("Seek failed for {Time}, using first frame", timePosition);
                // Continue anyway - will get closest frame
            }

            ffmpeg.avcodec_flush_buffers(codecContext);

            // Decode frame
            var frame = DecodeFrameAtPosition(formatContext, codecContext, videoStreamIndex, timePosition);

            if (frame == null)
                throw new InvalidDataException($"Failed to decode frame at {timePosition}");

            return frame;
        }
        finally
        {
            if (codecContext != null)
            {
                var ctx = codecContext;
                ffmpeg.avcodec_free_context(&ctx);
            }

            if (formatContext != null)
            {
                var ctx = formatContext;
                ffmpeg.avformat_close_input(&ctx);
            }
        }
    }

    private VideoFrame? DecodeFrameAtPosition(
        AVFormatContext* formatContext,
        AVCodecContext* codecContext,
        int videoStreamIndex,
        TimeSpan targetTime)
    {
        var packet = ffmpeg.av_packet_alloc();
        var frame = ffmpeg.av_frame_alloc();

        try
        {
            // Read frames until we find target or close to it
            while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
            {
                if (packet->stream_index == videoStreamIndex)
                {
                    if (ffmpeg.avcodec_send_packet(codecContext, packet) == 0)
                    {
                        if (ffmpeg.avcodec_receive_frame(codecContext, frame) == 0)
                        {
                            // Convert frame to RGB24 and create VideoFrame
                            var videoFrame = ConvertFrameToRGB24(frame, codecContext, targetTime);

                            ffmpeg.av_packet_unref(packet);
                            return videoFrame;
                        }
                    }
                }

                ffmpeg.av_packet_unref(packet);
            }

            return null;
        }
        finally
        {
            ffmpeg.av_packet_free(&packet);
            ffmpeg.av_frame_free(&frame);
        }
    }

    private VideoFrame ConvertFrameToRGB24(AVFrame* frame, AVCodecContext* codecContext, TimeSpan timePosition)
    {
        var width = codecContext->width;
        var height = codecContext->height;

        // Create scaled frame for RGB24
        var scaledFrame = ffmpeg.av_frame_alloc();
        try
        {
            scaledFrame->width = width;
            scaledFrame->height = height;
            scaledFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;

            ffmpeg.av_frame_get_buffer(scaledFrame, 32);

            // Create scaling context
            var swsContext = ffmpeg.sws_getContext(
                width, height, codecContext->pix_fmt,
                width, height, AVPixelFormat.AV_PIX_FMT_RGB24,
                ffmpeg.SWS_BILINEAR, null, null, null);

            if (swsContext == null)
                throw new InvalidOperationException("Failed to create scaling context");

            try
            {
                // Scale to RGB24
                ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, height,
                    scaledFrame->data, scaledFrame->linesize);

                // Copy to managed byte array
                var imageData = new byte[width * height * 3]; // RGB24
                var srcPtr = (byte*)scaledFrame->data[0];
                var linesize = scaledFrame->linesize[0];

                fixed (byte* dstPtr = imageData)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(
                            srcPtr + (y * linesize),
                            dstPtr + (y * width * 3),
                            width * 3,
                            width * 3);
                    }
                }

                return new VideoFrame
                {
                    TimePosition = timePosition,
                    ImageData = imageData,
                    Width = width,
                    Height = height
                };
            }
            finally
            {
                ffmpeg.sws_freeContext(swsContext);
            }
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

        _isDisposed = true;
    }
}
