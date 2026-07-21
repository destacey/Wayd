using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Wayd.Integrations.AzureDevOps.Tests.Support;

/// <summary>
/// Minimal in-memory <see cref="IMemoryCache"/> double: enough of the real semantics (get/set,
/// absolute expiration) to test <see cref="AzureDevOpsService"/>'s iteration cache without pulling
/// in the concrete Microsoft.Extensions.Caching.Memory package as a test dependency.
/// </summary>
public sealed class FakeMemoryCache : IMemoryCache
{
    private sealed record Entry(object? Value, DateTimeOffset? AbsoluteExpiration);

    private readonly Dictionary<object, Entry> _entries = [];

    public int SetCallCount { get; private set; }

    public ICacheEntry CreateEntry(object key) => new FakeCacheEntry(this, key);

    public void Remove(object key) => _entries.Remove(key);

    public bool TryGetValue(object key, out object? value)
    {
        if (_entries.TryGetValue(key, out var entry) &&
            (entry.AbsoluteExpiration is null || entry.AbsoluteExpiration > DateTimeOffset.UtcNow))
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Dispose() { }

    private void Store(object key, object? value, DateTimeOffset? absoluteExpiration)
    {
        SetCallCount++;
        _entries[key] = new Entry(value, absoluteExpiration);
    }

    private sealed class FakeCacheEntry(FakeMemoryCache cache, object key) : ICacheEntry
    {
        public object Key { get; } = key;
        public object? Value { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public IList<IChangeToken> ExpirationTokens { get; } = [];
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = [];
        public CacheItemPriority Priority { get; set; }
        public long? Size { get; set; }

        public void Dispose()
        {
            var absolute = AbsoluteExpiration ?? (AbsoluteExpirationRelativeToNow.HasValue
                ? DateTimeOffset.UtcNow + AbsoluteExpirationRelativeToNow.Value
                : null);
            cache.Store(Key, Value, absolute);
        }
    }
}
