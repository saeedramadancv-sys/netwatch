namespace NetWatch.Domain.Enums;

/// <summary>
/// Lifecycle of an outage record.
/// </summary>
public enum IncidentStatus
{
    /// <summary>Currently failing, nobody has claimed it.</summary>
    Open = 0,

    /// <summary>Still failing, but an operator has acknowledged they are on it.</summary>
    Acknowledged = 1,

    /// <summary>Target recovered; the incident was closed automatically.</summary>
    Resolved = 2
}
