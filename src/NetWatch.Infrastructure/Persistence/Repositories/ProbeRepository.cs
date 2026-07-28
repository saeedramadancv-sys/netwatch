using Microsoft.EntityFrameworkCore;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;

namespace NetWatch.Infrastructure.Persistence.Repositories;

public class ProbeRepository(AppDbContext context) : IProbeRepository
{
    public Task<Probe?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.Probes.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Probe?> GetWithDeviceAsync(int id, CancellationToken cancellationToken = default) =>
        context.Probes
            .Include(p => p.Device)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<int>> GetDueProbeIdsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        await context.Probes
            .AsNoTracking()
            .Where(p => p.IsEnabled && p.Device.IsEnabled)
            // Expressed with AddSeconds rather than a TimeSpan subtraction so the whole
            // predicate translates to SQL (DATEADD / julianday) instead of loading every
            // probe and filtering in memory.
            .Where(p => p.LastCheckedAtUtc == null
                        || p.LastCheckedAtUtc.Value.AddSeconds(p.IntervalSeconds) <= nowUtc)
            // Longest-waiting first, so a backlog drains fairly instead of starving
            // whichever probe happens to sort last by id.
            .OrderBy(p => p.LastCheckedAtUtc ?? DateTime.MinValue)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Probe>> ListAsync(
        int? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Probes.AsNoTracking().AsQueryable();

        if (deviceId is not null)
        {
            query = query.Where(p => p.DeviceId == deviceId);
        }

        return await query.OrderBy(p => p.DeviceId).ThenBy(p => p.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Probe>> ListWithDevicesAsync(CancellationToken cancellationToken = default) =>
        await context.Probes
            .Include(p => p.Device)
            .AsNoTracking()
            .OrderBy(p => p.Device.Name)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public void Add(Probe probe) => context.Probes.Add(probe);

    public void Remove(Probe probe) => context.Probes.Remove(probe);
}
