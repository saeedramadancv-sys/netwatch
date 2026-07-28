using NetWatch.Domain.Enums;

namespace NetWatch.Domain.Entities;

/// <summary>
/// The outcome of feeding one check result into a probe's state machine.
/// Returned instead of mutating shared state elsewhere, so callers (the scheduler,
/// the SignalR broadcaster, the incident writer) all react to one authoritative
/// description of what just changed.
/// </summary>
/// <param name="PreviousState">State before the result was applied.</param>
/// <param name="NewState">State after the result was applied.</param>
/// <param name="ShouldOpenIncident">The probe just entered a problem state from a healthy one.</param>
/// <param name="ShouldResolveIncident">The probe just returned to healthy from a problem state.</param>
/// <param name="ShouldEscalateIncident">Already in a problem state, but the severity got worse (degraded to down).</param>
/// <param name="Severity">Severity implied by <paramref name="NewState"/>, null when healthy.</param>
public sealed record ProbeTransition(
    ProbeState PreviousState,
    ProbeState NewState,
    bool ShouldOpenIncident,
    bool ShouldResolveIncident,
    bool ShouldEscalateIncident,
    IncidentSeverity? Severity)
{
    public bool StateChanged => PreviousState != NewState;

    /// <summary>True when any listener needs to be notified about this result.</summary>
    public bool IsNoteworthy => StateChanged || ShouldOpenIncident || ShouldResolveIncident || ShouldEscalateIncident;

    /// <summary>
    /// A probe is "in a problem state" when it is down or degraded. Both warrant an
    /// incident record; they differ only in severity.
    /// </summary>
    public static bool IsProblem(ProbeState state) => state is ProbeState.Down or ProbeState.Degraded;

    public static IncidentSeverity? SeverityFor(ProbeState state) => state switch
    {
        ProbeState.Down => IncidentSeverity.Critical,
        ProbeState.Degraded => IncidentSeverity.Warning,
        _ => null
    };
}
