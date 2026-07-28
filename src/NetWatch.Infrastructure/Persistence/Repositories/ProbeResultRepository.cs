using Microsoft.EntityFrameworkCore;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Common.Models;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Repositories;

public class ProbeResultRepository(AppDbContext context) : IProbeResultRepository
{
    public void Add(ProbeResult result) => context.ProbeResults.Add(result);

    public async Task<IReadOnlyList<ProbeResult>> GetRangeAsync(
        int probeId,
        DateTime fromUtc,
        DateTime toUtc,
        int maxPoints = 500,
        CancellationToken cancellationToken = default)
    {
        // Take the newest maxPoints rows, then flip back to chronological order for the
        // chart. Taking the oldest instead would silently show a stale window whenever
        // the range contains more points than the cap.
        var newest = await context.ProbeResults
            .AsNoTracking()
            .Where(r => r.ProbeId == probeId && r.CheckedAtUtc >= fromUtc && r.CheckedAtUtc <= toUtc)
            .OrderByDescending(r => r.CheckedAtUtc)
            .Take(maxPoints)
            .ToListAsync(cancellationToken);

        newest.Reverse();
        return newest;
    }

    public async Task<UptimeStats> GetStatsAsync(
        int probeId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        // One round trip, aggregated server-side. Nullable aggregates are used so an
        // empty window returns nulls instead of throwing on Average over no rows.
        var stats = await context.ProbeResults
            .AsNoTracking()
            .Where(r => r.ProbeId == probeId && r.CheckedAtUtc >= fromUtc && r.CheckedAtUtc <= toUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Successful = g.Count(r => r.IsSuccess),
                Average = g.Average(r => r.ResponseTimeMs),
                Min = g.Min(r => r.ResponseTimeMs),
                Max = g.Max(r => r.ResponseTimeMs)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats is null
            ? UptimeStats.Empty
            : new UptimeStats(stats.Total, stats.Successful, stats.Average, stats.Min, stats.Max);
    }

    public async Task<UptimeStats> GetOverallStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var stats = await context.ProbeResults
            .AsNoTracking()
            .Where(r => r.CheckedAtUtc >= fromUtc && r.CheckedAtUtc <= toUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Successful = g.Count(r => r.IsSuccess),
                Average = g.Average(r => r.ResponseTimeMs),
                Min = g.Min(r => r.ResponseTimeMs),
                Max = g.Max(r => r.ResponseTimeMs)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats is null
            ? UptimeStats.Empty
            : new UptimeStats(stats.Total, stats.Successful, stats.Average, stats.Min, stats.Max);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // ExecuteDeleteAsync issues a set-based DELETE instead of loading entities into
        // the change tracker. Bounded by batchSize so retention never takes a long lock.
        return await context.ProbeResults
            .Where(r => r.CheckedAtUtc < cutoffUtc)
            .OrderBy(r => r.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
