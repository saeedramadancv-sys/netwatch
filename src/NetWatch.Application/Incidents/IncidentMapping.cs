using NetWatch.Domain.Entities;

namespace NetWatch.Application.Incidents;

public static class IncidentMapping
{
    /// <summary>
    /// Projects an incident to its API shape. The probe and its device must be loaded:
    /// an incident that only says "probe 47 is down" is useless on screen.
    /// </summary>
    /// <param name="nowUtc">
    /// Used to compute the running duration of an unresolved incident. Passed in rather
    /// than read from the clock so the value is deterministic in tests.
    /// </param>
    public static IncidentResponse ToResponse(this Incident incident, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var probe = incident.Probe;

        return new IncidentResponse(
            incident.Id,
            incident.ProbeId,
            probe?.DeviceId ?? 0,
            probe?.Device?.Name ?? string.Empty,
            probe?.Describe() ?? string.Empty,
            probe?.Type ?? default,
            incident.Status,
            incident.Severity,
            incident.Cause,
            incident.FailedChecks,
            incident.StartedAtUtc,
            incident.ResolvedAtUtc,
            incident.AcknowledgedByUserId,
            incident.AcknowledgedAtUtc,
            incident.DurationAt(nowUtc).TotalSeconds);
    }
}
