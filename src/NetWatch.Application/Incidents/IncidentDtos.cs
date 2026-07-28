using NetWatch.Domain.Enums;

namespace NetWatch.Application.Incidents;

public sealed record IncidentResponse(
    int Id,
    int ProbeId,
    int DeviceId,
    string DeviceName,
    string Target,
    ProbeType ProbeType,
    IncidentStatus Status,
    IncidentSeverity Severity,
    string Cause,
    int FailedChecks,
    DateTime StartedAtUtc,
    DateTime? ResolvedAtUtc,
    string? AcknowledgedByUserId,
    DateTime? AcknowledgedAtUtc,
    double DurationSeconds);

public sealed record IncidentQuery(
    IncidentStatus? Status = null,
    int? DeviceId = null,
    DateTime? FromUtc = null,
    int Take = 100);
