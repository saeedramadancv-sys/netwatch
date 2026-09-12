using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Dashboard;

namespace NetWatch.Application.Monitoring;

/// <summary>
/// Decorator that retires the cached dashboard summary whenever a monitoring event
/// makes it wrong, then forwards the event to the real notifier.
///
/// Invalidation is deliberately not wired to every check. The scheduler fires a
/// <see cref="ProbeCheckedNotification"/> on every probe on every tick; treating each
/// one as a change would retire the cache several times a second and leave the cache
/// doing nothing but adding a round trip. Only a transition — or an incident opening or
/// resolving — can alter a number on the summary, so only those invalidate.
/// </summary>
public sealed class CacheInvalidatingMonitoringNotifier(
    IMonitoringNotifier inner,
    ICacheService cache) : IMonitoringNotifier
{
    public async Task ProbeCheckedAsync(ProbeCheckedNotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.State != notification.PreviousState)
        {
            await cache.InvalidateAsync(CachedDashboardService.CacheFamily, cancellationToken);
        }

        await inner.ProbeCheckedAsync(notification, cancellationToken);
    }

    public async Task IncidentOpenedAsync(IncidentNotification notification, CancellationToken cancellationToken = default)
    {
        await cache.InvalidateAsync(CachedDashboardService.CacheFamily, cancellationToken);
        await inner.IncidentOpenedAsync(notification, cancellationToken);
    }

    public async Task IncidentResolvedAsync(IncidentNotification notification, CancellationToken cancellationToken = default)
    {
        await cache.InvalidateAsync(CachedDashboardService.CacheFamily, cancellationToken);
        await inner.IncidentResolvedAsync(notification, cancellationToken);
    }
}
