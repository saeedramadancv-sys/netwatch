using Microsoft.AspNetCore.SignalR;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Monitoring;

namespace NetWatch.Api.Hubs;

/// <summary>
/// SignalR implementation of the monitoring feed.
///
/// Every method swallows its own failures on purpose. Notification is a side effect of a
/// check that has already been committed: if a socket is gone or the hub is busy, the
/// measurement is still recorded, and letting a broadcast exception bubble up would abort
/// the monitoring sweep over a cosmetic problem.
/// </summary>
public class SignalRMonitoringNotifier(
    IHubContext<MonitoringHub> hub,
    ILogger<SignalRMonitoringNotifier> logger) : IMonitoringNotifier
{
    public Task ProbeCheckedAsync(ProbeCheckedNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(MonitoringHub.ProbeCheckedEvent, notification, cancellationToken);

    public Task IncidentOpenedAsync(IncidentNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(MonitoringHub.IncidentOpenedEvent, notification, cancellationToken);

    public Task IncidentResolvedAsync(IncidentNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(MonitoringHub.IncidentResolvedEvent, notification, cancellationToken);

    private async Task SendAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.All.SendAsync(eventName, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast {Event} to connected clients.", eventName);
        }
    }
}
