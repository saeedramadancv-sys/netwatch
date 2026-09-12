using NetWatch.Application.Common.Interfaces;

namespace NetWatch.Application.Dashboard;

/// <summary>
/// Caching decorator over <see cref="DashboardService"/>.
///
/// The summary is the most expensive read in the application — four repository round
/// trips, one of them an aggregate over the whole probe-result table — and every open
/// dashboard requests it. It is also the one read where serving a stale value is a
/// correctness bug, not just an inconvenience: a monitoring wall showing "all up"
/// thirty seconds after a host went down is worse than no wall at all.
///
/// So the entry is not left to expire on its own. The TTL is a backstop measured in
/// seconds, and <see cref="CacheInvalidatingMonitoringNotifier"/> retires the family the
/// moment a probe changes state or an incident opens or resolves. Between those events
/// nothing on the summary can change, which is exactly when caching is free.
/// </summary>
public sealed class CachedDashboardService(
    IDashboardService inner,
    ICacheService cache,
    TimeSpan timeToLive) : IDashboardService
{
    /// <summary>Cache-key family retired on every monitoring state change.</summary>
    public const string CacheFamily = "dashboard:summary";

    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        int uptimeWindowHours = 24,
        CancellationToken cancellationToken = default)
    {
        var version = await cache.GetVersionAsync(CacheFamily, cancellationToken);

        // windowHours is caller-supplied and therefore unbounded, so it belongs in the key
        // rather than being cached over: two clients watching different windows must not
        // read each other's totals.
        var key = $"{CacheFamily}:{version}:{uptimeWindowHours}";

        return await cache.GetOrCreateAsync(
            key,
            ct => inner.GetSummaryAsync(uptimeWindowHours, ct),
            timeToLive,
            cancellationToken);
    }
}
