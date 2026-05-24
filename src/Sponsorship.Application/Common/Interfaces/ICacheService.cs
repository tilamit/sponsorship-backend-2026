namespace Sponsorship.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken ct = default);

    void Remove(string key);
    void RemoveByPrefix(string prefix);
}
