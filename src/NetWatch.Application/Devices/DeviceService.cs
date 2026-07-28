using NetWatch.Application.Common.Exceptions;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Devices;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceResponse>> ListAsync(string? search, DeviceCategory? category, string? site, CancellationToken cancellationToken = default);

    Task<DeviceDetailResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListSitesAsync(CancellationToken cancellationToken = default);

    Task<DeviceDetailResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);

    Task<DeviceDetailResponse> UpdateAsync(int id, UpdateDeviceRequest request, CancellationToken cancellationToken = default);

    Task SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class DeviceService(IDeviceRepository devices, IUnitOfWork unitOfWork) : IDeviceService
{
    public async Task<IReadOnlyList<DeviceResponse>> ListAsync(
        string? search,
        DeviceCategory? category,
        string? site,
        CancellationToken cancellationToken = default)
    {
        var results = await devices.ListAsync(search, category, site, cancellationToken);
        return [.. results.Select(d => d.ToResponse())];
    }

    public async Task<DeviceDetailResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetWithProbesAsync(id, cancellationToken)
                     ?? throw new NotFoundException(nameof(Device), id);

        return device.ToDetailResponse();
    }

    public Task<IReadOnlyList<string>> ListSitesAsync(CancellationToken cancellationToken = default) =>
        devices.ListSitesAsync(cancellationToken);

    public async Task<DeviceDetailResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked before constructing the entity so the caller gets a 409 about the
        // duplicate rather than a 400 from an unrelated validation rule.
        await EnsureHostnameIsFreeAsync(request.Hostname, null, cancellationToken);

        var device = new Device(request.Name, request.Hostname, request.Category, request.Site, request.Description);

        devices.Add(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return device.ToDetailResponse();
    }

    public async Task<DeviceDetailResponse> UpdateAsync(int id, UpdateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var device = await devices.GetWithProbesAsync(id, cancellationToken)
                     ?? throw new NotFoundException(nameof(Device), id);

        await EnsureHostnameIsFreeAsync(request.Hostname, id, cancellationToken);

        device.Update(request.Name, request.Hostname, request.Category, request.Site, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return device.ToDetailResponse();
    }

    public async Task SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException(nameof(Device), id);

        device.SetEnabled(enabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var device = await devices.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException(nameof(Device), id);

        // Probes, their results and their incidents cascade. Deleting a device really does
        // discard its history; disabling it is the non-destructive option.
        devices.Remove(device);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureHostnameIsFreeAsync(string hostname, int? excludeDeviceId, CancellationToken cancellationToken)
    {
        var trimmed = hostname?.Trim() ?? string.Empty;

        if (await devices.HostnameExistsAsync(trimmed, excludeDeviceId, cancellationToken))
        {
            throw new ConflictException($"'{trimmed}' is already registered as a device.");
        }
    }
}
