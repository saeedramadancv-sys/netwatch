using Microsoft.Extensions.Logging;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Monitoring;

/// <summary>
/// The monitoring pipeline for a single check.
///
/// Order matters and is deliberate:
///   1. run the probe (the only step that touches the network)
///   2. let the probe grade the raw outcome against its own latency policy
///   3. record the measurement
///   4. apply the state machine, which decides whether anything changed
///   5. open, escalate or resolve the incident the transition implies
///   6. commit, then notify
///
/// Notification comes last, after the commit, so a dashboard never shows an incident
/// that the database does not have.
/// </summary>
public class ProbeCheckService(
    IProbeRunner runner,
    IProbeResultRepository results,
    IIncidentRepository incidents,
    IUnitOfWork unitOfWork,
    IMonitoringNotifier notifier,
    TimeProvider timeProvider,
    ILogger<ProbeCheckService> logger) : IProbeCheckService
{
    public async Task<ProbeTransition> CheckAsync(Probe probe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var checkedAt = timeProvider.GetUtcNow().UtcDateTime;

        var execution = await runner.RunAsync(probe, cancellationToken);
        var outcome = probe.Grade(execution.Outcome, execution.ResponseTimeMs);

        results.Add(new ProbeResult(
            probe.Id,
            checkedAt,
            outcome,
            execution.ResponseTimeMs,
            execution.StatusCode,
            execution.ErrorMessage));

        var transition = probe.RecordResult(outcome, execution.ResponseTimeMs, checkedAt);

        var incident = await ApplyIncidentAsync(probe, transition, execution.ErrorMessage, checkedAt, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAsync(probe, transition, outcome, execution.ResponseTimeMs, checkedAt, incident, cancellationToken);

        // Describe() builds a string and the enum arguments box, both on the hot path:
        // this runs for every probe on every tick. Guarding means none of that is paid
        // when Information is not enabled.
        if (transition.IsNoteworthy && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Probe {ProbeId} ({Target}) moved {From} -> {To} after {Outcome}.",
                probe.Id, probe.Describe(), transition.PreviousState, transition.NewState, outcome);
        }

        return transition;
    }

    /// <summary>
    /// Translates a state transition into incident bookkeeping. Returns the incident the
    /// clients should be told about, or null when nothing incident-worthy happened.
    /// </summary>
    private async Task<Incident?> ApplyIncidentAsync(
        Probe probe,
        ProbeTransition transition,
        string? errorMessage,
        DateTime checkedAt,
        CancellationToken cancellationToken)
    {
        var cause = BuildCause(transition, errorMessage);

        if (transition.ShouldOpenIncident)
        {
            // Defensive: a crash between opening an incident and committing the probe
            // state could leave an orphan open incident, so reuse one if it exists
            // rather than creating a second for the same outage.
            var existing = await incidents.GetActiveForProbeAsync(probe.Id, cancellationToken);
            if (existing is not null)
            {
                existing.Escalate(transition.Severity ?? IncidentSeverity.Critical, cause);
                return existing;
            }

            var opened = new Incident(probe.Id, checkedAt, transition.Severity ?? IncidentSeverity.Critical, cause);
            incidents.Add(opened);
            return opened;
        }

        if (transition.ShouldResolveIncident)
        {
            var active = await incidents.GetActiveForProbeAsync(probe.Id, cancellationToken);
            active?.Resolve(checkedAt);
            return active;
        }

        if (transition.ShouldEscalateIncident)
        {
            var active = await incidents.GetActiveForProbeAsync(probe.Id, cancellationToken);
            active?.Escalate(transition.Severity ?? IncidentSeverity.Critical, cause);
            return active;
        }

        // Still broken but nothing changed: keep the failure count and latest cause
        // current so the incidents page shows why it is *still* failing, not only why
        // it first broke.
        if (ProbeTransition.IsProblem(transition.NewState))
        {
            var active = await incidents.GetActiveForProbeAsync(probe.Id, cancellationToken);
            active?.RecordFailure(cause);
        }

        return null;
    }

    private async Task NotifyAsync(
        Probe probe,
        ProbeTransition transition,
        ProbeOutcome outcome,
        double? responseTimeMs,
        DateTime checkedAt,
        Incident? incident,
        CancellationToken cancellationToken)
    {
        var target = probe.Describe();
        var deviceName = probe.Device?.Name ?? target;

        await notifier.ProbeCheckedAsync(
            new ProbeCheckedNotification(
                probe.Id,
                probe.DeviceId,
                deviceName,
                target,
                probe.Type,
                transition.NewState,
                transition.PreviousState,
                outcome,
                responseTimeMs,
                checkedAt),
            cancellationToken);

        if (incident is null)
        {
            return;
        }

        var payload = new IncidentNotification(
            incident.Id,
            probe.Id,
            probe.DeviceId,
            deviceName,
            target,
            incident.Severity,
            incident.Status,
            incident.Cause,
            incident.StartedAtUtc,
            incident.ResolvedAtUtc);

        if (transition.ShouldResolveIncident)
        {
            await notifier.IncidentResolvedAsync(payload, cancellationToken);
        }
        else
        {
            await notifier.IncidentOpenedAsync(payload, cancellationToken);
        }
    }

    private static string BuildCause(ProbeTransition transition, string? errorMessage) =>
        string.IsNullOrWhiteSpace(errorMessage)
            ? $"Probe is {transition.NewState.ToString().ToLowerInvariant()}."
            : errorMessage;
}
