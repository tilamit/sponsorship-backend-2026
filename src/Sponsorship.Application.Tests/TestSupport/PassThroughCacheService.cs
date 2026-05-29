using System.Collections.Concurrent;
using Sponsorship.Application.Common.Interfaces;

namespace Sponsorship.Application.Tests.TestSupport;

/// A real (non-mocked) ICacheService that always invokes the factory and records
/// invalidation calls. Mocking the generic GetOrCreateAsync is awkward, so the
/// service tests use this double to exercise the surrounding logic while still
/// asserting that Remove / RemoveByPrefix were called.
public sealed class PassThroughCacheService : ICacheService
{
    public ConcurrentBag<string> Removed { get; } = new();
    public ConcurrentBag<string> RemovedPrefixes { get; } = new();
    public int FactoryInvocations { get; private set; }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken ct = default)
    {
        FactoryInvocations++;
        return await factory(ct);
    }

    public void Remove(string key) => Removed.Add(key);

    public void RemoveByPrefix(string prefix) => RemovedPrefixes.Add(prefix);
}
