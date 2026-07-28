using NetWatch.Domain.Enums;

namespace NetWatch.Application.Probes;

/// <summary>
/// A configured check plus its current health.
/// </summary>
/// <param name="Target">Rendered target, e.g. "10.0.0.1:443" or "https://example.com/health".</param>
public sealed record ProbeResponse(
    int Id,
    int DeviceId,
    string DeviceName,
    ProbeType Type,
    string Target,
    int? Port,
    string? HttpPath,
    bool UseHttps,
    int ExpectedStatusCode,
    int IntervalSeconds,
    int TimeoutMs,
    int FailureThreshold,
    int RecoveryThreshold,
    int? DegradedLatencyMs,
    bool IsEnabled,
    ProbeState State,
    ProbeOutcome? LastOutcome,
    double? LastResponseTimeMs,
    DateTime? LastCheckedAtUtc);

public sealed record CreateProbeRequest(
    ProbeType Type,
    int IntervalSeconds = 60,
    int TimeoutMs = 5_000,
    int? Port = null,
    string? HttpPath = null,
    bool UseHttps = true,
    int ExpectedStatusCode = 200,
    int FailureThreshold = 3,
    int RecoveryThreshold = 2,
    int? DegradedLatencyMs = null);

/// <summary>
/// Probe type is absent on purpose: changing an ICMP check into an HTTP check would make
/// its accumulated history meaningless. Delete and recreate instead.
/// </summary>
public sealed record UpdateProbeRequest(
    int IntervalSeconds,
    int TimeoutMs,
    int? Port,
    string? HttpPath,
    bool UseHttps,
    int ExpectedStatusCode,
    int FailureThreshold,
    int RecoveryThreshold,
    int? DegradedLatencyMs);

public sealed record ProbeHistoryPoint(
    DateTime CheckedAtUtc,
    bool IsSuccess,
    ProbeOutcome Outcome,
    double? ResponseTimeMs,
    int? StatusCode,
    string? ErrorMessage);

/// <summary>
/// Aggregates for a time window.
/// </summary>
/// <param name="UptimePercent">Null when the window contains no checks at all.</param>
/// <param name="P95ResponseTimeMs">
/// Computed over the returned sample rather than the full window, so it reflects exactly
/// the points drawn on the chart.
/// </param>
public sealed record UptimeSummary(
    int TotalChecks,
    int SuccessfulChecks,
    int FailedChecks,
    double? UptimePercent,
    double? AverageResponseTimeMs,
    double? P95ResponseTimeMs,
    double? MinResponseTimeMs,
    double? MaxResponseTimeMs);

public sealed record ProbeHistoryResponse(
    int ProbeId,
    string Target,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ProbeHistoryPoint> Points,
    UptimeSummary Summary);
