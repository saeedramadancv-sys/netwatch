using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NetWatch.Api.Hubs;

/// <summary>
/// Live monitoring feed for connected dashboards.
///
/// The hub is push-only: clients receive <c>ProbeChecked</c>, <c>IncidentOpened</c> and
/// <c>IncidentResolved</c> and never invoke anything. Every mutation still goes through
/// the REST API, so authorisation is enforced in exactly one place.
///
/// Requiring authentication here matters — without it, an anonymous socket would receive
/// the hostnames and health of the entire monitored estate.
/// </summary>
[Authorize]
public class MonitoringHub : Hub
{
    /// <summary>Event name clients subscribe to for individual check results.</summary>
    public const string ProbeCheckedEvent = "ProbeChecked";

    public const string IncidentOpenedEvent = "IncidentOpened";

    public const string IncidentResolvedEvent = "IncidentResolved";
}
