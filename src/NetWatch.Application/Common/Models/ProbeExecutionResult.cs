using NetWatch.Domain.Enums;

namespace NetWatch.Application.Common.Models;

/// <summary>
/// What one probe attempt observed on the wire.
///
/// Executors report raw facts only — reachable or not, how long it took, which HTTP
/// status came back. They never decide whether that counts as "degraded"; that grading
/// depends on per-probe configuration and belongs to the domain, not to the socket code.
/// </summary>
/// <param name="Outcome">Raw classification. Never <see cref="ProbeOutcome.Degraded"/>.</param>
/// <param name="ResponseTimeMs">Round-trip time when the target answered.</param>
/// <param name="StatusCode">HTTP status, for HTTP probes.</param>
/// <param name="ErrorMessage">Short diagnostic shown in the incident cause.</param>
public sealed record ProbeExecutionResult(
    ProbeOutcome Outcome,
    double? ResponseTimeMs,
    int? StatusCode = null,
    string? ErrorMessage = null)
{
    public static ProbeExecutionResult Success(double responseTimeMs, int? statusCode = null) =>
        new(ProbeOutcome.Success, responseTimeMs, statusCode);

    public static ProbeExecutionResult Failure(ProbeOutcome outcome, string errorMessage, double? responseTimeMs = null, int? statusCode = null) =>
        new(outcome, responseTimeMs, statusCode, errorMessage);
}
