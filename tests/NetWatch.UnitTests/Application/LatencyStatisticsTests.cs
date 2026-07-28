using FluentAssertions;
using NetWatch.Application.Common.Models;
using NetWatch.Application.Monitoring;

namespace NetWatch.UnitTests.Application;

public class LatencyStatisticsTests
{
    [Fact]
    public void PercentileOfAnEmptySample_IsNullRatherThanZero()
    {
        LatencyStatistics.Percentile([], 95).Should().BeNull();
    }

    [Fact]
    public void PercentileOfASingleValue_IsThatValue()
    {
        LatencyStatistics.Percentile([42], 95).Should().Be(42);
    }

    [Fact]
    public void P95_PicksTheNinetyFifthOfAHundredOrderedSamples()
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i);

        LatencyStatistics.Percentile(values, 95).Should().Be(95);
    }

    [Fact]
    public void PercentileDoesNotDependOnInputOrder()
    {
        double[] values = [500, 10, 300, 20, 100];

        LatencyStatistics.Percentile(values, 100).Should().Be(500);
        LatencyStatistics.Percentile(values, 20).Should().Be(10);
    }

    [Fact]
    public void P95_ExposesATailThatTheAverageHides()
    {
        // The reason the dashboard reports p95 at all: 94 fast responses and 6 terrible
        // ones average out to a healthy-looking 309ms, while p95 reports the 5s reality
        // that 6% of users actually experienced.
        var values = Enumerable.Repeat(10d, 94).Concat(Enumerable.Repeat(5_000d, 6)).ToArray();

        LatencyStatistics.Average(values).Should().BeApproximately(309.4, 0.1);
        LatencyStatistics.Percentile(values, 95).Should().Be(5_000);
    }

    [Fact]
    public void ATailSmallerThanFivePercent_DoesNotMoveP95()
    {
        // The counterpart to the test above, and the property that makes p95 useful:
        // a single outlier in a hundred samples is noise, not a trend.
        var values = Enumerable.Repeat(10d, 99).Append(5_000d).ToArray();

        LatencyStatistics.Percentile(values, 95).Should().Be(10);
        LatencyStatistics.Percentile(values, 100).Should().Be(5_000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void AnOutOfRangePercentileIsRejected(double percentile)
    {
        var act = () => LatencyStatistics.Percentile([1, 2, 3], percentile);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AverageOfAnEmptySample_IsNull()
    {
        LatencyStatistics.Average([]).Should().BeNull();
    }
}

public class UptimeStatsTests
{
    [Fact]
    public void AWindowWithNoChecks_ReportsNullUptimeNotZero()
    {
        // "We did not measure" and "it was down" must not look the same on a dashboard.
        UptimeStats.Empty.UptimePercent.Should().BeNull();
    }

    [Fact]
    public void UptimeIsSuccessfulChecksOverTotal()
    {
        var stats = new UptimeStats(TotalChecks: 200, SuccessfulChecks: 197, 25, 10, 90);

        stats.UptimePercent.Should().Be(98.5);
        stats.FailedChecks.Should().Be(3);
    }

    [Fact]
    public void UptimeIsRoundedToTwoDecimals()
    {
        var stats = new UptimeStats(TotalChecks: 3, SuccessfulChecks: 2, null, null, null);

        stats.UptimePercent.Should().Be(66.67);
    }

    [Fact]
    public void APerfectWindowIsExactlyOneHundred()
    {
        var stats = new UptimeStats(TotalChecks: 1_440, SuccessfulChecks: 1_440, 12, 8, 30);

        stats.UptimePercent.Should().Be(100);
    }
}
