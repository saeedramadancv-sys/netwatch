namespace NetWatch.Application.Monitoring;

/// <summary>
/// Latency summaries that are awkward to express portably in SQL.
///
/// Averages are computed in the database (see <c>IProbeResultRepository.GetStatsAsync</c>)
/// because they aggregate cheaply. Percentiles are computed here, over the sampled points
/// already fetched for the chart, because PERCENTILE_CONT is not available on every
/// provider this project targets and a second full scan would not be worth it.
/// </summary>
public static class LatencyStatistics
{
    /// <summary>
    /// Nearest-rank percentile. p95 answers "95% of responses were at least this fast",
    /// which is the number worth alerting on — an average hides the tail that users feel.
    /// </summary>
    /// <param name="values">Latency samples in milliseconds. Order does not matter.</param>
    /// <param name="percentile">Between 0 and 100 exclusive of 0.</param>
    /// <returns>Null when there are no samples.</returns>
    public static double? Percentile(IEnumerable<double> values, double percentile)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (percentile is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be in (0, 100].");
        }

        var sorted = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        // Nearest-rank: rank = ceil(p/100 * N), clamped into the array bounds.
        var rank = (int)Math.Ceiling(percentile / 100d * sorted.Length);
        var index = Math.Clamp(rank - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    /// <summary>
    /// Mean of the samples, or null when there are none. Kept separate from the database
    /// average so in-memory chart data can be summarised without another round trip.
    /// </summary>
    public static double? Average(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        double sum = 0;
        var count = 0;

        foreach (var value in values)
        {
            if (double.IsNaN(value))
            {
                continue;
            }

            sum += value;
            count++;
        }

        return count == 0 ? null : sum / count;
    }
}
