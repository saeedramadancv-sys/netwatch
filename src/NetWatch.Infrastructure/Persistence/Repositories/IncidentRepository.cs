using Microsoft.EntityFrameworkCore;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Persistence.Repositories;

public class IncidentRepository(AppDbContext context) : IIncidentRepository
{
    public Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.Incidents
            .Include(i => i.Probe)
            .ThenInclude(p => p.Device)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Incident?> GetActiveForProbeAsync(int probeId, CancellationToken cancellationToken = default) =>
        context.Incidents.FirstOrDefaultAsync(
            i => i.ProbeId == probeId && i.Status != IncidentStatus.Resolved,
            cancellationToken);

    public async Task<IReadOnlyList<Incident>> ListAsync(
        IncidentStatus? status = null,
        int? deviceId = null,
        DateTime? fromUtc = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = context.Incidents
            .Include(i => i.Probe)
            .ThenInclude(p => p.Device)
            .AsNoTracking()
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (deviceId is not null)
        {
            query = query.Where(i => i.Probe.DeviceId == deviceId);
        }

        if (fromUtc is not null)
        {
            query = query.Where(i => i.StartedAtUtc >= fromUtc);
        }

        return await query
            .OrderByDescending(i => i.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        context.Incidents.CountAsync(i => i.Status != IncidentStatus.Resolved, cancellationToken);

    public void Add(Incident incident) => context.Incidents.Add(incident);
}
