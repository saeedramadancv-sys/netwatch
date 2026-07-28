using NetWatch.Domain.Enums;

namespace NetWatch.Domain.Entities;

/// <summary>
/// A single measurement. This is the high-volume table: one row per probe per
/// interval, so a hundred probes on a 60s interval writes ~144k rows a day.
/// Rows are append-only and never updated, which is why this does not inherit
/// <see cref="Common.BaseEntity"/> and uses a <see cref="long"/> key.
/// </summary>
public class ProbeResult
{
    // EF Core materialisation constructor.
    private ProbeResult()
    {
    }

    public ProbeResult(
        int probeId,
        DateTime checkedAtUtc,
        ProbeOutcome outcome,
        double? responseTimeMs,
        int? statusCode = null,
        string? errorMessage = null)
    {
        ProbeId = probeId;
        CheckedAtUtc = checkedAtUtc;
        Outcome = outcome;
        ResponseTimeMs = responseTimeMs;
        StatusCode = statusCode;
        ErrorMessage = errorMessage?.Length > 500 ? errorMessage[..500] : errorMessage;
        IsSuccess = IsReachable(outcome);
    }

    public long Id { get; private set; }

    public int ProbeId { get; private set; }

    public Probe Probe { get; private set; } = null!;

    public DateTime CheckedAtUtc { get; private set; }

    public ProbeOutcome Outcome { get; private set; }

    /// <summary>
    /// Denormalised reachability flag. Persisted rather than computed so uptime
    /// aggregates translate to a plain indexed SQL COUNT instead of pulling every
    /// row into memory to evaluate an enum.
    /// </summary>
    public bool IsSuccess { get; private set; }

    public double? ResponseTimeMs { get; private set; }

    /// <summary>HTTP status code, when the probe was an HTTP check.</summary>
    public int? StatusCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// A degraded response still means the target answered, so it counts towards
    /// availability. Uptime measures reachability; latency is reported separately.
    /// </summary>
    public static bool IsReachable(ProbeOutcome outcome) =>
        outcome is ProbeOutcome.Success or ProbeOutcome.Degraded;
}
