namespace NetWatch.Application.Common.Models;

/// <summary>
/// Aggregated health of a single probe over a time window.
/// Computed in the database rather than in memory: a 30-day window on a 30-second
/// probe is ~86k rows, which is cheap to COUNT and expensive to materialise.
/// </summary>
/// <param name="TotalChecks">Rows in the window.</param>
/// <param name="SuccessfulChecks">Rows where the target answered (success or degraded).</param>
/// <param name="AverageResponseTimeMs">Mean latency across answering checks only.</param>
/// <param name="MinResponseTimeMs">Fastest answering check.</param>
/// <param name="MaxResponseTimeMs">Slowest answering check.</param>
public sealed record UptimeStats(
    int TotalChecks,
    int SuccessfulChecks,
    double? AverageResponseTimeMs,
    double? MinResponseTimeMs,
    double? MaxResponseTimeMs)
{
    public static UptimeStats Empty { get; } = new(0, 0, null, null, null);

    public int FailedChecks => TotalChecks - SuccessfulChecks;

    /// <summary>
    /// Availability as a percentage, rounded to two decimals.
    /// A window with no checks reports null rather than 0% or 100% — "we did not
    /// measure" and "it was down" must not look the same on a dashboard.
    /// </summary>
    public double? UptimePercent =>
        TotalChecks == 0 ? null : Math.Round(SuccessfulChecks * 100d / TotalChecks, 2);
}
