using System;
using System.Collections.Generic;
using System.IO;
using Bref.FFmpeg;
using Bref.Models;
using FFmpeg.AutoGen;
using Serilog;

namespace Bref.Services;

/// <summary>
/// Generates video thumbnails at regular intervals using FFmpeg.
/// </summary>
public unsafe class ThumbnailGenerator
{
    /// <summary>
    /// Generates thumbnails from video at specified intervals.
    /// </summary>
    /// <param name="videoFilePath">Path to video file</param>
    /// <param name="interval">Time interval between thumbnails</param>
    /// <param name="width">Thumbnail width in pixels</param>
    /// <param name="height">Thumbnail height in pixels</param>
    /// <returns>List of thumbnail data</returns>
    /// <exception cref="FileNotFoundException">Thrown if file doesn't exist</exception>
    public List<ThumbnailData> Generate(
        string videoFilePath,
        TimeSpan interval,
        int width,
        int height)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        var thumbnails = new List<ThumbnailData>();

        try
        {
            // Initialize FFmpeg
            FFmpegSetup.Initialize();

            AVFormatContext* formatContext = null;
            if (ffmpeg.avformat_open_input(&formatContext, videoFilePath, null, null) != 0)
            {
                throw new InvalidDataException("Could not open video file");
            }

            try
            {
                if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
                {
                    throw new InvalidDataException("Could not find stream information");
                }

                // Find video stream
                int videoStreamIndex = -1;
                AVCodecContext* codecContext = null;

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
                    throw new InvalidDataException("No video stream found");
                }

                var codecParams = formatContext->streams[videoStreamIndex]->codecpar;
                var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
                if (codec == null)
                {
                    throw new InvalidDataException("Codec not found");
                }

                codecContext = ffmpeg.avcodec_alloc_context3(codec);
                if (codecContext == null)
                {
                    throw new OutOfMemoryException("Could not allocate codec context");
                }

                try
                {
                    ffmpeg.avcodec_parameters_to_context(codecContext, codecParams);

                    if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                    {
                        throw new InvalidDataException("Could not open codec");
                    }

                    // Calculate total duration
                    var stream = formatContext->streams[videoStreamIndex];
                    var duration = TimeSpan.FromSeconds(stream->duration * ffmpeg.av_q2d(stream->time_base));

                    // Generate thumbnails at intervals
                    var currentTime = TimeSpan.Zero;
                    while (currentTime < duration)
                    {
                        var thumbnail = ExtractFrameAtTime(formatContext, codecContext, videoStreamIndex, currentTime, width, height);
                        if (thumbnail != null)
                        {
                            thumbnails.Add(thumbnail);
                        }

                        currentTime += interval;
                    }
                }
                finally
                {
                    if (codecContext != null)
                    {
                        var ctx = codecContext;
                        ffmpeg.avcodec_free_context(&ctx);
                    }
                }
            }
            finally
            {
                var ctx = formatContext;
                ffmpeg.avformat_close_input(&ctx);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate thumbnails for: {FilePath}", videoFilePath);
            throw new InvalidDataException($"Failed to generate thumbnails: {ex.Message}", ex);
        }

        return thumbnails;
    }

    /// <summary>
    /// Generates a single thumbnail at a specific time.
    /// </summary>
    public ThumbnailData? GenerateSingle(string videoFilePath, TimeSpan time, int width, int height)
    {
        if (!File.Exists(videoFilePath))
        {
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);
        }

        try
        {
            FFmpegSetup.Initialize();

            AVFormatContext* formatContext = null;
            if (ffmpeg.avformat_open_input(&formatContext, videoFilePath, null, null) != 0)
            {
                return null;
            }

            try
            {
                if (ffmpeg.avformat_find_stream_info(formatContext, null) < 0)
                {
                    return null;
                }

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
                    return null;

                var codecParams = formatContext->streams[videoStreamIndex]->codecpar;
                var codec = ffmpeg.avcodec_find_decoder(codecParams->codec_id);
                if (codec == null)
                    return null;

                AVCodecContext* codecContext = ffmpeg.avcodec_alloc_context3(codec);
                if (codecContext == null)
                    return null;

                try
                {
                    ffmpeg.avcodec_parameters_to_context(codecContext, codecParams);
                    if (ffmpeg.avcodec_open2(codecContext, codec, null) < 0)
                        return null;

                    return ExtractFrameAtTime(formatContext, codecContext, videoStreamIndex, time, width, height);
                }
                finally
                {
                    if (codecContext != null)
                    {
                        var ctx = codecContext;
                        ffmpeg.avcodec_free_context(&ctx);
                    }
                }
            }
            finally
            {
                var ctx = formatContext;
                ffmpeg.avformat_close_input(&ctx);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate single thumbnail at {Time} for: {FilePath}", time, videoFilePath);
            return null;
        }
    }

    private ThumbnailData? ExtractFrameAtTime(
        AVFormatContext* formatContext,
        AVCodecContext* codecContext,
        int videoStreamIndex,
        TimeSpan targetTime,
        int width,
        int height)
    {
        try
        {
            // Seek to target time
            var stream = formatContext->streams[videoStreamIndex];
            long timestamp = (long)(targetTime.TotalSeconds / ffmpeg.av_q2d(stream->time_base));

            if (ffmpeg.av_seek_frame(formatContext, videoStreamIndex, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
            {
                Log.Warning("Failed to seek to {Time} in video", targetTime);
                return null;
            }

            ffmpeg.avcodec_flush_buffers(codecContext);

            var packet = ffmpeg.av_packet_alloc();
            var frame = ffmpeg.av_frame_alloc();
            var scaledFrame = ffmpeg.av_frame_alloc();

            try
            {
                // Read frames until we find one at or after target time
                while (ffmpeg.av_read_frame(formatContext, packet) >= 0)
                {
                    if (packet->stream_index == videoStreamIndex)
                    {
                        if (ffmpeg.avcodec_send_packet(codecContext, packet) == 0)
                        {
                            if (ffmpeg.avcodec_receive_frame(codecContext, frame) == 0)
                            {
                                // Scale frame to thumbnail size
                                scaledFrame->width = width;
                                scaledFrame->height = height;
                                scaledFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;

                                ffmpeg.av_frame_get_buffer(scaledFrame, 32);

                                var swsContext = ffmpeg.sws_getContext(
                                    codecContext->width, codecContext->height, codecContext->pix_fmt,
                                    width, height, AVPixelFormat.AV_PIX_FMT_RGB24,
                                    ffmpeg.SWS_BILINEAR, null, null, null);

                                if (swsContext == null)
                                {
                                    throw new InvalidOperationException("Could not create scaling context");
                                }

                                try
                                {
                                    ffmpeg.sws_scale(swsContext, frame->data, frame->linesize, 0, codecContext->height,
                                        scaledFrame->data, scaledFrame->linesize);

                                    // Convert RGB24 frame to JPEG byte array
                                    var imageData = FrameToJpeg(scaledFrame, width, height);

                                    return new ThumbnailData
                                    {
                                        TimePosition = targetTime,
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
                        }
                    }

                    ffmpeg.av_packet_unref(packet);
                }
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);
                ffmpeg.av_frame_free(&frame);
                ffmpeg.av_frame_free(&scaledFrame);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract frame at {Time}", targetTime);
        }

        return null;
    }

    private byte[] FrameToJpeg(AVFrame* frame, int width, int height)
    {
        // JPEG encoding using SkiaSharp - convert RGB24 to BGRA8888 format
        using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque);

        var ptr = (byte*)bitmap.GetPixels();
        var dataPtr = (byte*)frame->data[0];
        var linesize = frame->linesize[0];

        // Convert RGB24 (3 bytes/pixel) to BGRA (4 bytes/pixel)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcOffset = (y * linesize) + (x * 3);
                int dstOffset = (y * width * 4) + (x * 4);

                ptr[dstOffset + 0] = dataPtr[srcOffset + 2]; // B (from R)
                ptr[dstOffset + 1] = dataPtr[srcOffset + 1]; // G
                ptr[dstOffset + 2] = dataPtr[srcOffset + 0]; // R (from B)
                ptr[dstOffset + 3] = 255; // A (alpha)
            }
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 85);

        return data.ToArray();
    }
}
