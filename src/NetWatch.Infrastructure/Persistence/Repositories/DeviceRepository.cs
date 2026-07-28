using Microsoft.EntityFrameworkCore;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Infrastructure.Persistence.Repositories;

public class DeviceRepository(AppDbContext context) : IDeviceRepository
{
    public Task<Device?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Device?> GetWithProbesAsync(int id, CancellationToken cancellationToken = default) =>
        context.Devices
            .Include(d => d.Probes)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Device>> ListAsync(
        string? search = null,
        DeviceCategory? category = null,
        string? site = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Devices
            .Include(d => d.Probes)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.Name.Contains(term) || d.Hostname.Contains(term));
        }

        if (category is not null)
        {
            query = query.Where(d => d.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(site))
        {
            query = query.Where(d => d.Site == site);
        }

        return await query
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HostnameExistsAsync(
        string hostname,
        int? excludeDeviceId = null,
        CancellationToken cancellationToken = default) =>
        context.Devices.AnyAsync(
            d => d.Hostname == hostname && (excludeDeviceId == null || d.Id != excludeDeviceId),
            cancellationToken);

    public async Task<IReadOnlyList<string>> ListSitesAsync(CancellationToken cancellationToken = default) =>
        await context.Devices
            .Where(d => d.Site != null)
            .Select(d => d.Site!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

    public void Add(Device device) => context.Devices.Add(device);

    public void Remove(Device device) => context.Devices.Remove(device);
}
