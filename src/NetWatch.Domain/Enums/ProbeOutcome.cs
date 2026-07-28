namespace NetWatch.Domain.Enums;

/// <summary>
/// The precise reason a probe attempt ended the way it did.
/// Distinguishing failure modes matters: a DNS failure and a refused connection
/// point at very different root causes even though both render the target "down".
/// </summary>
public enum ProbeOutcome
{
    /// <summary>Target responded as expected.</summary>
    Success = 0,

    /// <summary>Target responded, but slower than the configured degraded threshold.</summary>
    Degraded = 1,

    /// <summary>No response within the timeout window.</summary>
    Timeout = 2,

    /// <summary>Host reachable but actively refused the connection (nothing listening on the port).</summary>
    ConnectionRefused = 3,

    /// <summary>Hostname could not be resolved.</summary>
    DnsFailure = 4,

    /// <summary>No route to the host, or the network is unreachable.</summary>
    Unreachable = 5,

    /// <summary>HTTP responded, but with a status code other than the expected one.</summary>
    UnexpectedStatusCode = 6,

    /// <summary>TLS handshake or certificate validation failed.</summary>
    TlsFailure = 7,

    /// <summary>Anything the probe could not classify.</summary>
    Error = 99
}
