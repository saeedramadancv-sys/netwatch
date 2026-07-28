using NetWatch.Application.Common.Exceptions;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Application.Monitoring;
using NetWatch.Domain.Entities;

namespace NetWatch.Application.Probes;

public interface IProbeService
{
    Task<ProbeResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ProbeResponse> CreateAsync(int deviceId, CreateProbeRequest request, CancellationToken cancellationToken = default);

    Task<ProbeResponse> UpdateAsync(int id, UpdateProbeRequest request, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ProbeHistoryResponse> GetHistoryAsync(int id, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken cancellationToken = default);

    /// <summary>Runs the check immediately instead of waiting for its next scheduled slot.</summary>
    Task<ProbeResponse> RunNowAsync(int id, CancellationToken cancellationToken = default);
}

public class ProbeService(
    IProbeRepository probes,
    IDeviceRepository devices,
    IProbeResultRepository results,
    IProbeCheckService checkService,
    IUnitOfWork unitOfWork) : IProbeService
{
    public async Task<ProbeResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var probe = await probes.GetWithDeviceAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        return probe.ToResponse();
    }

    public async Task<ProbeResponse> CreateAsync(int deviceId, CreateProbeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var device = await devices.GetWithProbesAsync(deviceId, cancellationToken)
                     ?? throw new NotFoundException(nameof(Device), deviceId);

        var probe = new Probe(
            request.Type,
            request.IntervalSeconds,
            request.TimeoutMs,
            request.Port,
            request.HttpPath,
            request.UseHttps,
            request.ExpectedStatusCode,
            request.FailureThreshold,
            request.RecoveryThreshold,
            request.DegradedLatencyMs);

        // Added through the aggregate root so the duplicate-probe rule is enforced by the
        // domain rather than re-implemented here.
        device.AddProbe(probe);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return probe.ToResponse();
    }

    public async Task<ProbeResponse> UpdateAsync(int id, UpdateProbeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var probe = await probes.GetWithDeviceAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        probe.Update(
            request.IntervalSeconds,
            request.TimeoutMs,
            request.Port,
            request.HttpPath,
            request.UseHttps,
            request.ExpectedStatusCode,
            request.FailureThreshold,
            request.RecoveryThreshold,
            request.DegradedLatencyMs);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return probe.ToResponse();
    }

    public async Task SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var probe = await probes.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        probe.SetEnabled(enabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var probe = await probes.GetByIdAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        probes.Remove(probe);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProbeHistoryResponse> GetHistoryAsync(
        int id,
        DateTime fromUtc,
        DateTime toUtc,
        int maxPoints,
        CancellationToken cancellationToken = default)
    {
        var probe = await probes.GetWithDeviceAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        var points = await results.GetRangeAsync(id, fromUtc, toUtc, maxPoints, cancellationToken);

        // Counts and averages come from the database over the whole window; the percentile
        // is computed over the sample actually returned. Mixing the two is intentional and
        // documented on UptimeSummary so nobody reads p95 as a full-window figure.
        var stats = await results.GetStatsAsync(id, fromUtc, toUtc, cancellationToken);

        var latencies = points
            .Where(p => p.ResponseTimeMs.HasValue)
            .Select(p => p.ResponseTimeMs!.Value);

        var summary = BuildSummary(stats, latencies);

        return new ProbeHistoryResponse(
            probe.Id,
            probe.Describe(),
            fromUtc,
            toUtc,
            [.. points.Select(p => p.ToHistoryPoint())],
            summary);
    }

    public async Task<ProbeResponse> RunNowAsync(int id, CancellationToken cancellationToken = default)
    {
        var probe = await probes.GetWithDeviceAsync(id, cancellationToken)
                    ?? throw new NotFoundException(nameof(Probe), id);

        // Goes through the same pipeline as a scheduled check, so a manual run updates
        // state, history and incidents exactly like an automatic one.
        await checkService.CheckAsync(probe, cancellationToken);

        return probe.ToResponse();
    }

    private static UptimeSummary BuildSummary(UptimeStats stats, IEnumerable<double> sampledLatencies) =>
        new(
            stats.TotalChecks,
            stats.SuccessfulChecks,
            stats.FailedChecks,
            stats.UptimePercent,
            stats.AverageResponseTimeMs,
            LatencyStatistics.Percentile(sampledLatencies, 95),
            stats.MinResponseTimeMs,
            stats.MaxResponseTimeMs);
}
