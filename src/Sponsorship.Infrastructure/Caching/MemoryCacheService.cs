using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Infrastructure.Caching;

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    // Track all live keys so we can support prefix-based invalidation.
    private static readonly ConcurrentDictionary<string, byte> _keys = new();

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue<T>(key, out var cached)) return cached;

        var value = await factory(ct);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration,
            Size = 1
        };
        options.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (k, _, _, _) =>
            {
                if (k is string s) _keys.TryRemove(s, out _);
            }
        });

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);
        return value;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            Remove(key);
        }
    }
}
