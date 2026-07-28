using NetWatch.Application.Common.Exceptions;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;

namespace NetWatch.Application.Incidents;

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentResponse>> ListAsync(IncidentQuery query, CancellationToken cancellationToken = default);

    Task<IncidentResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<IncidentResponse> AcknowledgeAsync(int id, string userId, CancellationToken cancellationToken = default);
}

public class IncidentService(
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IIncidentService
{
    public async Task<IReadOnlyList<IncidentResponse>> ListAsync(IncidentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var items = await incidents.ListAsync(
            query.Status,
            query.DeviceId,
            query.FromUtc,
            query.Take,
            cancellationToken);

        return [.. items.Select(i => i.ToResponse(now))];
    }

    public async Task<IncidentResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException(nameof(Incident), id);

        return incident.ToResponse(timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<IncidentResponse> AcknowledgeAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var incident = await incidents.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException(nameof(Incident), id);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Acknowledging a resolved incident throws from the domain; the API turns that
        // into a 400 rather than silently doing nothing.
        incident.Acknowledge(userId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return incident.ToResponse(now);
    }
}
