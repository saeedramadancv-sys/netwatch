using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetWatch.Application.Common.Interfaces;
using NetWatch.Application.Dashboard;
using NetWatch.Application.Monitoring;
using NetWatch.Domain.Enums;
using NetWatch.Infrastructure.Caching;
using NSubstitute;

namespace NetWatch.UnitTests.Application;

public class DashboardCachingTests
{
    private static DistributedCacheService RealCache() =>
        new DistributedCacheService(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<DistributedCacheService>.Instance);

    private static DashboardSummaryResponse Summary(int downCount = 0) =>
        new(DeviceCount: 1, ProbeCount: 1, UpCount: 1 - downCount, DegradedCount: 0,
            DownCount: downCount, UnknownCount: 0, ActiveIncidentCount: downCount,
            OverallUptimePercent: downCount == 0 ? 100 : 0, Probes: [], RecentIncidents: []);

    private static ProbeCheckedNotification Check(ProbeState state, ProbeState previous) =>
        new(ProbeId: 1, DeviceId: 1, DeviceName: "web", Target: "example.com",
            Type: ProbeType.Http, State: state, PreviousState: previous,
            Outcome: ProbeOutcome.Success, ResponseTimeMs: 12, CheckedAtUtc: DateTime.UtcNow);

    [Fact]
    public async Task RepeatedReadsHitTheCacheInsteadOfTheDatabase()
    {
        var inner = Substitute.For<IDashboardService>();
        inner.GetSummaryAsync(24, Arg.Any<CancellationToken>()).Returns(Summary());

        var cached = new CachedDashboardService(inner, RealCache(), TimeSpan.FromMinutes(1));

        await cached.GetSummaryAsync(24);
        await cached.GetSummaryAsync(24);
        await cached.GetSummaryAsync(24);

        await inner.Received(1).GetSummaryAsync(24, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DifferentUptimeWindowsAreCachedSeparately()
    {
        var inner = Substitute.For<IDashboardService>();
        inner.GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Summary());

        var cached = new CachedDashboardService(inner, RealCache(), TimeSpan.FromMinutes(1));

        await cached.GetSummaryAsync(24);
        await cached.GetSummaryAsync(1);

        await inner.Received(1).GetSummaryAsync(24, Arg.Any<CancellationToken>());
        await inner.Received(1).GetSummaryAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AProbeGoingDownIsVisibleOnTheNextRead_NotAfterTheTtl()
    {
        // The behaviour the whole design exists for. Without invalidation this test reads
        // "all up" for the full TTL after the outage started.
        var cache = RealCache();
        var inner = Substitute.For<IDashboardService>();
        inner.GetSummaryAsync(24, Arg.Any<CancellationToken>()).Returns(Summary());

        var cached = new CachedDashboardService(inner, cache, TimeSpan.FromMinutes(10));
        var notifier = new CacheInvalidatingMonitoringNotifier(Substitute.For<IMonitoringNotifier>(), cache);

        (await cached.GetSummaryAsync(24)).DownCount.Should().Be(0);

        inner.GetSummaryAsync(24, Arg.Any<CancellationToken>()).Returns(Summary(downCount: 1));
        await notifier.ProbeCheckedAsync(Check(ProbeState.Down, previous: ProbeState.Up));

        (await cached.GetSummaryAsync(24)).DownCount.Should().Be(1);
    }

    [Fact]
    public async Task ACheckThatChangesNothingLeavesTheCacheAlone()
    {
        // Every probe reports on every tick. Invalidating on all of them would retire the
        // entry several times a second and leave the cache a pure cost.
        var cache = RealCache();
        var inner = Substitute.For<IDashboardService>();
        inner.GetSummaryAsync(24, Arg.Any<CancellationToken>()).Returns(Summary());

        var cached = new CachedDashboardService(inner, cache, TimeSpan.FromMinutes(10));
        var notifier = new CacheInvalidatingMonitoringNotifier(Substitute.For<IMonitoringNotifier>(), cache);

        await cached.GetSummaryAsync(24);

        for (var tick = 0; tick < 20; tick++)
        {
            await notifier.ProbeCheckedAsync(Check(ProbeState.Up, previous: ProbeState.Up));
        }

        await cached.GetSummaryAsync(24);

        await inner.Received(1).GetSummaryAsync(24, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EveryNotificationStillReachesTheRealNotifier()
    {
        var inner = Substitute.For<IMonitoringNotifier>();
        var notifier = new CacheInvalidatingMonitoringNotifier(inner, RealCache());

        var check = Check(ProbeState.Up, previous: ProbeState.Up);
        await notifier.ProbeCheckedAsync(check);

        await inner.Received(1).ProbeCheckedAsync(check, Arg.Any<CancellationToken>());
    }
}
