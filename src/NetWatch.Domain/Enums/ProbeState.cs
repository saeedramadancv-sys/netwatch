namespace NetWatch.Domain.Enums;

/// <summary>
/// The debounced health of a probe.
/// This is deliberately NOT the outcome of the last single check: a probe only
/// transitions to <see cref="Down"/> after a configured number of consecutive
/// failures, so a one-off dropped packet does not raise an incident.
/// </summary>
public enum ProbeState
{
    /// <summary>Never checked yet, or checks were reset.</summary>
    Unknown = 0,

    /// <summary>Responding within the expected latency budget.</summary>
    Up = 1,

    /// <summary>Responding, but slower than the degraded latency threshold.</summary>
    Degraded = 2,

    /// <summary>Failed enough consecutive checks to be considered down.</summary>
    Down = 3
}
