namespace NetWatch.Domain.Enums;

/// <summary>
/// The network mechanism used to determine whether a target is reachable.
/// </summary>
public enum ProbeType
{
    /// <summary>ICMP echo request. Cheapest check, but frequently blocked by firewalls and PaaS hosts.</summary>
    Icmp = 0,

    /// <summary>TCP connect to a specific port. Proves a service is listening, not just that the host is up.</summary>
    Tcp = 1,

    /// <summary>HTTP(S) request against a path, asserting an expected status code.</summary>
    Http = 2
}
