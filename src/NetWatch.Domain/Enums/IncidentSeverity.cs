namespace NetWatch.Domain.Enums;

/// <summary>
/// How badly an incident degrades service, derived from the probe state that raised it.
/// </summary>
public enum IncidentSeverity
{
    /// <summary>Target still responds but is slower than its latency budget.</summary>
    Warning = 0,

    /// <summary>Target is not responding at all.</summary>
    Critical = 1
}
