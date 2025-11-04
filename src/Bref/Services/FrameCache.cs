using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bref.Models;
using Bref.Utilities;
using Serilog;

namespace Bref.Services;

/// <summary>
/// High-performance frame cache with LRU eviction and intelligent preloading.
/// Thread-safe: Multiple threads can call GetFrame() and PreloadFramesAsync() concurrently.
/// </summary>
public class FrameCache : IDisposable
{
    private readonly string _videoFilePath;
    private readonly LRUCache<long, VideoFrame> _cache;
    private readonly PersistentFrameDecoder _decoder;
    private readonly object _decodeLock = new object();
    private bool _isDisposed;

    // Frame granularity: Cache frames at 33ms intervals (30fps)
    private const long FrameGranularityTicks = 330000; // 33ms in ticks

    public FrameCache(string videoFilePath, int capacity = 60)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoFilePath);
        if (!File.Exists(videoFilePath))
            throw new FileNotFoundException($"Video file not found: {videoFilePath}", videoFilePath);

        _videoFilePath = videoFilePath;
        _cache = new LRUCache<long, VideoFrame>(capacity);
        _decoder = new PersistentFrameDecoder(videoFilePath);

        Log.Information("FrameCache initialized for {FilePath} with capacity {Capacity}", videoFilePath, capacity);
    }

    /// <summary>
    /// Gets frame at specified time position.
    /// Returns cached frame if available, otherwise decodes and caches.
    /// </summary>
    public VideoFrame GetFrame(TimeSpan timePosition)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Quantize time to frame granularity for cache key
        var cacheKey = QuantizeTime(timePosition);

        // Check cache
        var cachedFrame = _cache.Get(cacheKey);
        if (cachedFrame != null)
        {
            return cachedFrame;
        }

        // Cache miss - decode frame (thread-safe)
        lock (_decodeLock)
        {
            // Double-check cache after acquiring lock (another thread might have added it)
            cachedFrame = _cache.Get(cacheKey);
            if (cachedFrame != null)
            {
                return cachedFrame;
            }

            var frame = _decoder.DecodeFrameAt(TimeSpan.FromTicks(cacheKey));

            // Add to cache
            _cache.Add(cacheKey, frame);

            return frame;
        }
    }

    /// <summary>
    /// Preloads frames around a target time for smooth scrubbing.
    /// Asynchronously loads nearby frames into cache.
    /// </summary>
    public async Task PreloadFramesAsync(TimeSpan centerTime, int frameRadius = 5, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var tasks = new List<Task>();

        // Preload frames before and after center
        for (int i = -frameRadius; i <= frameRadius; i++)
        {
            if (i == 0) continue; // Center frame already loaded

            var offset = TimeSpan.FromTicks(i * FrameGranularityTicks);
            var targetTime = centerTime + offset;

            if (targetTime < TimeSpan.Zero)
                continue;

            var cacheKey = QuantizeTime(targetTime);

            // Skip if already cached
            if (_cache.ContainsKey(cacheKey))
                continue;

            // Decode asynchronously using GetFrame (thread-safe)
            tasks.Add(Task.Run(() =>
            {
                // Check cancellation before decoding
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    // Use GetFrame instead of direct decoder call - it's thread-safe
                    GetFrame(targetTime);
                }
                catch (Exception ex)
                {
                    // Don't log if operation was cancelled
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Log.Warning(ex, "Failed to preload frame at {Time}", targetTime);
                    }
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Quantizes time to frame granularity for cache key.
    /// E.g., 1.234s -> 1.200s (at 30fps)
    /// </summary>
    private long QuantizeTime(TimeSpan time)
    {
        return (time.Ticks / FrameGranularityTicks) * FrameGranularityTicks;
    }

    /// <summary>
    /// Clears all cached frames.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _cache.Clear();
        Log.Information("Frame cache cleared");
    }

    /// <summary>
    /// Gets current cache statistics.
    /// </summary>
    public (int Count, int Capacity) GetStats()
    {
        return (_cache.Count, _cache.Capacity);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        // Acquire decode lock to ensure no decode operations are in progress
        lock (_decodeLock)
        {
            _cache.Dispose();
            _decoder.Dispose();
            _isDisposed = true;
        }

        Log.Information("FrameCache disposed");
    }
}
