using NetWatch.Application.Monitoring;

namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Fan-out of live monitoring events to connected clients.
///
/// Abstracted so the monitoring pipeline has no compile-time dependency on SignalR:
/// the scheduler is unit tested against a stub, and swapping the transport later
/// (web push, a message bus) touches one class in the API layer.
/// </summary>
public interface IMonitoringNotifier
{
    Task ProbeCheckedAsync(ProbeCheckedNotification notification, CancellationToken cancellationToken = default);

    Task IncidentOpenedAsync(IncidentNotification notification, CancellationToken cancellationToken = default);

    Task IncidentResolvedAsync(IncidentNotification notification, CancellationToken cancellationToken = default);
}
