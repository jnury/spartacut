using Bref.Utilities;
using Xunit;

namespace Bref.Tests.Utilities;

public class LRUCacheTests
{
    [Fact]
    public void Get_WithExistingKey_ReturnsValue()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);
        var value = new TestDisposable("test");
        cache.Add("key1", value);

        // Act
        var result = cache.Get("key1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.Name);
    }

    [Fact]
    public void Get_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);

        // Act
        var result = cache.Get("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Add_WhenAtCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);
        var value1 = new TestDisposable("value1");
        var value2 = new TestDisposable("value2");
        var value3 = new TestDisposable("value3");
        var value4 = new TestDisposable("value4");

        cache.Add("key1", value1);
        cache.Add("key2", value2);
        cache.Add("key3", value3);

        // Act - Access key1 to make it recently used
        _ = cache.Get("key1");

        // Add key4 (should evict key2, the least recently used)
        cache.Add("key4", value4);

        // Assert
        Assert.Null(cache.Get("key2")); // Evicted
        Assert.NotNull(cache.Get("key1")); // Still present (recently used)
        Assert.NotNull(cache.Get("key3")); // Still present
        Assert.NotNull(cache.Get("key4")); // Newly added
        Assert.True(value2.IsDisposed); // Evicted value was disposed
    }

    [Fact]
    public void Add_WithExistingKey_DisposesOldValue()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);
        var oldValue = new TestDisposable("old");
        var newValue = new TestDisposable("new");

        cache.Add("key1", oldValue);

        // Act
        cache.Add("key1", newValue); // Update

        // Assert
        var result = cache.Get("key1");
        Assert.Equal("new", result?.Name);
        Assert.True(oldValue.IsDisposed);
        Assert.False(newValue.IsDisposed);
    }

    [Fact]
    public void Clear_DisposesAllItems()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);
        var values = new[]
        {
            new TestDisposable("value1"),
            new TestDisposable("value2"),
            new TestDisposable("value3")
        };

        cache.Add("key1", values[0]);
        cache.Add("key2", values[1]);
        cache.Add("key3", values[2]);

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(0, cache.Count);
        Assert.All(values, v => Assert.True(v.IsDisposed));
    }

    [Fact]
    public void Dispose_ClearsAndDisposesCache()
    {
        // Arrange
        var cache = new LRUCache<string, TestDisposable>(capacity: 3);
        var value = new TestDisposable("test");
        cache.Add("key1", value);

        // Act
        cache.Dispose();

        // Assert
        Assert.True(value.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => cache.Get("key1"));
    }

    private class TestDisposable : IDisposable
    {
        public string Name { get; }
        public bool IsDisposed { get; private set; }

        public TestDisposable(string name)
        {
            Name = name;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
