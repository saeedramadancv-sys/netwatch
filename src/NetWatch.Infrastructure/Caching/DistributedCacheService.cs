using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NetWatch.Application.Common.Interfaces;

namespace NetWatch.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/> over <see cref="IDistributedCache"/>, which is Redis when
/// a connection string is configured and an in-process store otherwise.
///
/// Every operation is wrapped: a cache is a latency optimisation, and an optimisation
/// that can return a 500 when its backing store blinks is a downgrade. On any cache
/// failure the read falls through to the source of truth and the request still succeeds,
/// one log entry heavier.
/// </summary>
public sealed class DistributedCacheService(
    IDistributedCache cache,
    ILogger<DistributedCacheService> logger) : ICacheService
{
    // Property names round-trip as declared. The cached payloads are internal DTOs, never
    // parsed by anything but this process, so matching the API's camelCase would only cost
    // a transform on both ends.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        try
        {
            var cached = await cache.GetStringAsync(key, cancellationToken);

            if (cached is not null)
            {
                var value = JsonSerializer.Deserialize<T>(cached, SerializerOptions);

                if (value is not null)
                {
                    return value;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Includes a payload written by an older build whose shape no longer
            // deserialises: rebuilding from source is always the safe answer.
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling through to source", key);
        }

        var fresh = await factory(cancellationToken);

        try
        {
            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(fresh, SerializerOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }

        return fresh;
    }

    public async Task<string> GetVersionAsync(string family, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        var versionKey = VersionKey(family);

        try
        {
            var current = await cache.GetStringAsync(versionKey, cancellationToken);

            if (!string.IsNullOrEmpty(current))
            {
                return current;
            }

            var seed = NewToken();
            await cache.SetStringAsync(versionKey, seed, cancellationToken);
            return seed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache version read failed for {CacheFamily}", family);

            // A token nothing else will reproduce. The read that follows misses and
            // rebuilds from source, which is the outcome we want when the cache is down.
            return NewToken();
        }
    }

    public async Task InvalidateAsync(string family, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        try
        {
            // Rotating the token orphans every key built from the old one; those entries
            // hold their own TTL and expire without further work. This is one write
            // regardless of how many keys the family had.
            await cache.SetStringAsync(VersionKey(family), NewToken(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Survivable: entries still carry the backstop TTL from CacheOptions, so a
            // failed invalidation delays fresh data by seconds rather than indefinitely.
            logger.LogWarning(ex, "Cache invalidation failed for {CacheFamily}", family);
        }
    }

    private static string VersionKey(string family) => $"{family}:__version";

    private static string NewToken() => Guid.NewGuid().ToString("N")[..12];
}
