using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Incidents;
using NetWatch.Application.Probes;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(int uptimeWindowHours = 24, CancellationToken cancellationToken = default);
}

public class DashboardService(
    IProbeRepository probes,
    IIncidentRepository incidents,
    IProbeResultRepository results,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        int uptimeWindowHours = 24,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = now.AddHours(-Math.Clamp(uptimeWindowHours, 1, 24 * 90));

        var all = await probes.ListWithDevicesAsync(cancellationToken);
        var activeIncidents = await incidents.CountActiveAsync(cancellationToken);

        var recent = await incidents.ListAsync(
            status: null,
            deviceId: null,
            fromUtc: null,
            take: 10,
            cancellationToken);

        var overallStats = await results.GetOverallStatsAsync(windowStart, now, cancellationToken);

        return new DashboardSummaryResponse(
            DeviceCount: all.Select(p => p.DeviceId).Distinct().Count(),
            ProbeCount: all.Count,
            UpCount: all.Count(p => p.State == ProbeState.Up),
            DegradedCount: all.Count(p => p.State == ProbeState.Degraded),
            DownCount: all.Count(p => p.State == ProbeState.Down),
            UnknownCount: all.Count(p => p.State == ProbeState.Unknown),
            ActiveIncidentCount: activeIncidents,
            // Fleet availability is total successes over total checks, not the mean of
            // per-probe percentages: averaging percentages would let a probe with three
            // checks weigh as much as one with three thousand.
            OverallUptimePercent: overallStats.UptimePercent,
            Probes: [.. all.Select(p => p.ToResponse())],
            RecentIncidents: [.. recent.Select(i => i.ToResponse(now))]);
    }
}
