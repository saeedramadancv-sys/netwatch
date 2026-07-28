using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;

namespace NetWatch.Application.Common.Interfaces;

public interface IProbeResultRepository
{
    void Add(ProbeResult result);

    /// <summary>
    /// Raw measurements inside a window, newest last, capped at <paramref name="maxPoints"/>
    /// so a chart request over a long range cannot pull a million rows into memory.
    /// </summary>
    Task<IReadOnlyList<ProbeResult>> GetRangeAsync(
        int probeId,
        DateTime fromUtc,
        DateTime toUtc,
        int maxPoints = 500,
        CancellationToken cancellationToken = default);

    Task<UptimeStats> GetStatsAsync(
        int probeId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fleet-wide aggregate across every probe in one query.
    ///
    /// Exists specifically to keep the dashboard off an N+1: calling
    /// <see cref="GetStatsAsync"/> once per probe would issue one round trip per monitored
    /// check, which is the difference between one query and several hundred on every page load.
    /// </summary>
    Task<UptimeStats> GetOverallStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes results older than the retention cutoff in bounded batches and returns how
    /// many rows went. Batched on purpose: one unbounded DELETE over months of history
    /// locks the table and can time out.
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default);
}
