using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Common.Interfaces;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Loads the device together with its probes, for detail views and probe edits.</summary>
    Task<Device?> GetWithProbesAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> ListAsync(
        string? search = null,
        DeviceCategory? category = null,
        string? site = null,
        CancellationToken cancellationToken = default);

    /// <summary>Guards against registering the same host twice under different names.</summary>
    Task<bool> HostnameExistsAsync(string hostname, int? excludeDeviceId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListSitesAsync(CancellationToken cancellationToken = default);

    void Add(Device device);

    void Remove(Device device);
}
