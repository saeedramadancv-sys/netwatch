using NetWatch.Domain.Enums;

namespace NetWatch.Application.Monitoring;

/// <summary>
/// Pushed to connected dashboards after every check so tiles update without polling.
/// </summary>
public sealed record ProbeCheckedNotification(
    int ProbeId,
    int DeviceId,
    string DeviceName,
    string Target,
    ProbeType Type,
    ProbeState State,
    ProbeState PreviousState,
    ProbeOutcome Outcome,
    double? ResponseTimeMs,
    DateTime CheckedAtUtc);

/// <summary>
/// Pushed when an incident opens, escalates or resolves.
/// </summary>
public sealed record IncidentNotification(
    int IncidentId,
    int ProbeId,
    int DeviceId,
    string DeviceName,
    string Target,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string Cause,
    DateTime StartedAtUtc,
    DateTime? ResolvedAtUtc);
