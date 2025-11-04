using System;
using SkiaSharp;

namespace Bref.Models;

/// <summary>
/// Represents a decoded video frame with image data.
/// Implements IDisposable for memory management.
/// </summary>
public class VideoFrame : IDisposable
{
    /// <summary>
    /// Time position of this frame in the video.
    /// </summary>
    public required TimeSpan TimePosition { get; init; }

    /// <summary>
    /// Frame image data as byte array (RGB24 format).
    /// </summary>
    public required byte[] ImageData { get; init; }

    /// <summary>
    /// Width of the frame in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Height of the frame in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Estimated memory size in bytes.
    /// </summary>
    public long MemorySize => ImageData.Length;

    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        // Image data will be GC'd
        // (In future optimization, could use unmanaged memory)
        _isDisposed = true;
    }

    /// <summary>
    /// Creates a VideoFrame from SkiaSharp bitmap.
    /// </summary>
    public static VideoFrame FromBitmap(SKBitmap bitmap, TimeSpan timePosition)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        var imageData = new byte[width * height * 3]; // RGB24

        var ptr = bitmap.GetPixels();
        unsafe
        {
            var srcPtr = (byte*)ptr;
            fixed (byte* dstPtr = imageData)
            {
                Buffer.MemoryCopy(srcPtr, dstPtr, imageData.Length, imageData.Length);
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

    /// <summary>
    /// Converts frame to SkiaSharp bitmap for rendering.
    /// </summary>
    public SKBitmap ToBitmap()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var bitmap = new SKBitmap(Width, Height, SKColorType.Rgb888x, SKAlphaType.Opaque);

        var ptr = bitmap.GetPixels();
        unsafe
        {
            var dstPtr = (byte*)ptr;
            fixed (byte* srcPtr = ImageData)
            {
                Buffer.MemoryCopy(srcPtr, dstPtr, ImageData.Length, ImageData.Length);
            }
        }

        return bitmap;
    }
}
