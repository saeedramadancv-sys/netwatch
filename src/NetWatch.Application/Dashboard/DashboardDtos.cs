using NetWatch.Application.Incidents;
using NetWatch.Application.Probes;

namespace NetWatch.Application.Dashboard;

/// <summary>
/// Everything the landing page needs, in one request. Deliberately a single endpoint
/// rather than five: the dashboard is the most-hit route in the app, and five parallel
/// calls each opening a connection is worse than one that aggregates.
/// </summary>
/// <param name="Probes">Every probe with its current state, for the status grid.</param>
public sealed record DashboardSummaryResponse(
    int DeviceCount,
    int ProbeCount,
    int UpCount,
    int DegradedCount,
    int DownCount,
    int UnknownCount,
    int ActiveIncidentCount,
    double? OverallUptimePercent,
    IReadOnlyList<ProbeResponse> Probes,
    IReadOnlyList<IncidentResponse> RecentIncidents);
