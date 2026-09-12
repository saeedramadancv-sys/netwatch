namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Read-through cache for expensive query results.
///
/// Abstracted here so the Application layer never references a cache provider: the
/// Redis implementation lives in Infrastructure, and a deployment without Redis
/// configured falls back to an in-process store without a single call site changing.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, invoking
    /// <paramref name="factory"/> and storing its result on a miss.
    /// </summary>
    /// <remarks>
    /// A cache failure must never take the endpoint down with it: implementations are
    /// expected to fall through to <paramref name="factory"/> when the backing store is
    /// unreachable, trading the latency win for staying available.
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current version token for a key family, creating one if absent.
    ///
    /// Callers fold the token into their cache keys so that <see cref="InvalidateAsync"/>
    /// can retire an unbounded family of keys in a single write.
    /// </summary>
    Task<string> GetVersionAsync(string family, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retires every key built from the current version of <paramref name="family"/>.
    ///
    /// IDistributedCache has no wildcard delete, and enumerating Redis keys to find
    /// matches is exactly the operation Redis documentation warns against in production.
    /// Rotating a version token instead makes invalidation one O(1) write; the orphaned
    /// entries carry their own TTL and expire on their own.
    /// </summary>
    Task InvalidateAsync(string family, CancellationToken cancellationToken = default);
}
