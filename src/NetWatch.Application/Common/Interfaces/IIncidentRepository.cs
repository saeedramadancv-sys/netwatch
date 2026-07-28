using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Common.Interfaces;

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The open (or acknowledged) incident for a probe, if any. There is at most one:
    /// a probe cannot be down twice at the same time.
    /// </summary>
    Task<Incident?> GetActiveForProbeAsync(int probeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Incident>> ListAsync(
        IncidentStatus? status = null,
        int? deviceId = null,
        DateTime? fromUtc = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    void Add(Incident incident);
}
