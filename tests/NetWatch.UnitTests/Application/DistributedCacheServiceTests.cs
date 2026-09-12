using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetWatch.Infrastructure.Caching;

namespace NetWatch.UnitTests.Application;

public class DistributedCacheServiceTests
{
    private static DistributedCacheService CreateService(IDistributedCache? cache = null) =>
        new(cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<DistributedCacheService>.Instance);

    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task AMissInvokesTheFactory_AndAHitDoesNot()
    {
        var service = CreateService();
        var calls = 0;

        Task<int> Factory(CancellationToken _) => Task.FromResult(++calls);

        (await service.GetOrCreateAsync("k", Factory, OneMinute)).Should().Be(1);
        (await service.GetOrCreateAsync("k", Factory, OneMinute)).Should().Be(1);

        calls.Should().Be(1);
    }

    [Fact]
    public async Task SeparateKeysDoNotShareAnEntry()
    {
        var service = CreateService();

        (await service.GetOrCreateAsync("a", _ => Task.FromResult("first"), OneMinute)).Should().Be("first");
        (await service.GetOrCreateAsync("b", _ => Task.FromResult("second"), OneMinute)).Should().Be("second");
    }

    [Fact]
    public async Task AVersionTokenIsStableUntilItIsRotated()
    {
        var service = CreateService();

        var first = await service.GetVersionAsync("family");
        var second = await service.GetVersionAsync("family");

        second.Should().Be(first, "keys built between invalidations must keep hitting the same entry");

        await service.InvalidateAsync("family");

        (await service.GetVersionAsync("family")).Should().NotBe(first);
    }

    [Fact]
    public async Task InvalidationRetiresEveryKeyBuiltFromTheOldToken()
    {
        var service = CreateService();
        var value = "before";

        async Task<string> Read()
        {
            var version = await service.GetVersionAsync("dash");
            return await service.GetOrCreateAsync($"dash:{version}", _ => Task.FromResult(value), OneMinute);
        }

        (await Read()).Should().Be("before");

        value = "after";
        (await Read()).Should().Be("before", "the entry is still valid until something invalidates it");

        await service.InvalidateAsync("dash");

        (await Read()).Should().Be("after");
    }

    [Fact]
    public async Task AFailingCacheStillServesTheRequestFromSource()
    {
        // The whole point of the try/catch in the service: a cache outage must cost
        // latency, not availability.
        var service = CreateService(new ThrowingCache());

        var result = await service.GetOrCreateAsync("k", _ => Task.FromResult("from source"), OneMinute);

        result.Should().Be("from source");
    }

    [Fact]
    public async Task InvalidationDoesNotThrowWhenTheCacheIsDown()
    {
        var service = CreateService(new ThrowingCache());

        var invalidate = async () => await service.InvalidateAsync("family");

        await invalidate.Should().NotThrowAsync();
    }

    private sealed class ThrowingCache : IDistributedCache
    {
        private static InvalidOperationException Fail() => new("cache unreachable");

        public byte[]? Get(string key) => throw Fail();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Fail();
        public void Refresh(string key) => throw Fail();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Fail();
        public void Remove(string key) => throw Fail();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Fail();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Fail();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Fail();
    }
}
