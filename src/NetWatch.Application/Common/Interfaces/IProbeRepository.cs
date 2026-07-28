using NetWatch.Domain.Entities;

namespace NetWatch.Application.Common.Interfaces;

public interface IProbeRepository
{
    Task<Probe?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Probe plus its device, because every executor needs the hostname.</summary>
    Task<Probe?> GetWithDeviceAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of the probes whose interval has elapsed at <paramref name="nowUtc"/>, oldest
    /// first. Only enabled probes on enabled devices qualify, so disabling a device
    /// silently parks every check it owns.
    ///
    /// Ids rather than entities: the scheduler runs each check in its own scope with its
    /// own <c>DbContext</c> (they are not thread safe), so entities loaded here could not
    /// be reused there anyway.
    /// </summary>
    Task<IReadOnlyList<int>> GetDueProbeIdsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Probe>> ListAsync(int? deviceId = null, CancellationToken cancellationToken = default);

    /// <summary>Every probe with its device, used to build the dashboard status grid.</summary>
    Task<IReadOnlyList<Probe>> ListWithDevicesAsync(CancellationToken cancellationToken = default);

    void Add(Probe probe);

    void Remove(Probe probe);
}
