using NetWatch.Domain.Common;
using NetWatch.Domain.Enums;

namespace NetWatch.Domain.Entities;

/// <summary>
/// An outage record, opened when a probe crosses into a problem state and closed
/// automatically when it recovers. One incident spans the whole outage rather than
/// one row per failed check, so "how many times did this break" stays answerable.
/// </summary>
public class Incident : BaseEntity
{
    // EF Core materialisation constructor.
    private Incident()
    {
        Cause = string.Empty;
    }

    public Incident(int probeId, DateTime startedAtUtc, IncidentSeverity severity, string cause)
    {
        ProbeId = probeId;
        StartedAtUtc = startedAtUtc;
        Severity = severity;
        Cause = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(cause, nameof(cause)), 500, nameof(cause));
        Status = IncidentStatus.Open;
        FailedChecks = 1;
    }

    public int ProbeId { get; private set; }

    public Probe Probe { get; private set; } = null!;

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public IncidentStatus Status { get; private set; }

    public IncidentSeverity Severity { get; private set; }

    /// <summary>Last known reason, e.g. "Timeout after 5000ms" or "Expected 200, got 502".</summary>
    public string Cause { get; private set; }

    /// <summary>How many checks failed while this incident was open. A rough blast-radius signal.</summary>
    public int FailedChecks { get; private set; }

    public string? AcknowledgedByUserId { get; private set; }

    public DateTime? AcknowledgedAtUtc { get; private set; }

    public bool IsActive => Status != IncidentStatus.Resolved;

    public TimeSpan? Duration => ResolvedAtUtc is null ? null : ResolvedAtUtc - StartedAtUtc;

    /// <summary>
    /// Live duration for an incident that has not been resolved yet.
    /// </summary>
    public TimeSpan DurationAt(DateTime nowUtc) => (ResolvedAtUtc ?? nowUtc) - StartedAtUtc;

    public void RecordFailure(string cause)
    {
        if (!IsActive)
        {
            throw new DomainException("Cannot record a failure against a resolved incident.");
        }

        FailedChecks++;
        Cause = Guard.AgainstTooLong(Guard.AgainstNullOrWhiteSpace(cause, nameof(cause)), 500, nameof(cause));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Raises severity when a degraded target goes fully down. Severity never drops
    /// while an incident is open — an outage that briefly improved was still an outage.
    /// </summary>
    public void Escalate(IncidentSeverity severity, string cause)
    {
        if (!IsActive)
        {
            throw new DomainException("Cannot escalate a resolved incident.");
        }

        if (severity > Severity)
        {
            Severity = severity;
        }

        RecordFailure(cause);
    }

    public void Acknowledge(string userId, DateTime atUtc)
    {
        if (!IsActive)
        {
            throw new DomainException("Cannot acknowledge a resolved incident.");
        }

        if (Status == IncidentStatus.Acknowledged)
        {
            return;
        }

        Status = IncidentStatus.Acknowledged;
        AcknowledgedByUserId = Guard.AgainstNullOrWhiteSpace(userId, nameof(userId));
        AcknowledgedAtUtc = atUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Resolve(DateTime resolvedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        if (resolvedAtUtc < StartedAtUtc)
        {
            throw new DomainException("An incident cannot be resolved before it started.");
        }

        Status = IncidentStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
