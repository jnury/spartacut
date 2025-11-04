using System;
using System.Collections.Generic;

namespace Bref.Utilities;

/// <summary>
/// Least Recently Used (LRU) cache with automatic eviction and disposal.
/// Thread-safe: All operations are protected by internal locking.
/// </summary>
/// <typeparam name="TKey">Cache key type</typeparam>
/// <typeparam name="TValue">Cache value type (must implement IDisposable)</typeparam>
public class LRUCache<TKey, TValue> : IDisposable
    where TKey : notnull
    where TValue : class, IDisposable
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new object();
    private bool _isDisposed;

    public LRUCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// Current number of items in cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Maximum capacity of cache.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets value from cache. Returns null if not found.
    /// Moves accessed item to front (most recently used).
    /// </summary>
    public TValue? Get(TKey key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var node))
                return null;

            // Move to front (most recently used)
            _lruList.Remove(node);
            _lruList.AddFirst(node);

            return node.Value.Value;
        }
    }

    /// <summary>
    /// Adds or updates value in cache.
    /// If cache is at capacity, evicts least recently used item (disposes it).
    /// If key exists, disposes old value and updates.
    /// </summary>
    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            // If key exists, remove old entry
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                existingNode.Value.Value.Dispose();
                _cache.Remove(key);
            }

            // Evict if at capacity
            if (_cache.Count >= _capacity)
            {
                EvictLeastRecentlyUsed();
            }

            // Add new entry at front
            var newItem = new CacheItem { Key = key, Value = value };
            var newNode = _lruList.AddFirst(newItem);
            _cache[key] = newNode;
        }
    }

    /// <summary>
    /// Checks if cache contains key.
    /// Does NOT update LRU order (use Get for that).
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            return _cache.ContainsKey(key);
        }
    }

    /// <summary>
    /// Removes item from cache and disposes it.
    /// Returns true if item was found and removed.
    /// </summary>
    public bool Remove(TKey key)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var node))
                return false;

            _lruList.Remove(node);
            _cache.Remove(key);
            node.Value.Value.Dispose();

            return true;
        }
    }

    /// <summary>
    /// Clears entire cache and disposes all items.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_lock)
        {
            foreach (var node in _lruList)
            {
                node.Value?.Dispose();
            }

            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Evicts least recently used item (tail of list).
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        if (_lruList.Last == null)
            return;

        var lruNode = _lruList.Last;
        _lruList.RemoveLast();
        _cache.Remove(lruNode.Value.Key);
        lruNode.Value.Value.Dispose();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
                return;

            // Clear will acquire lock again, but locks are reentrant for the same thread
            foreach (var node in _lruList)
            {
                node.Value?.Dispose();
            }

            _cache.Clear();
            _lruList.Clear();
            _isDisposed = true;
        }
    }

    private class CacheItem
    {
        public required TKey Key { get; init; }
        public required TValue Value { get; init; }
    }
}
