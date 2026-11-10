using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using ProductService.Api.Extensions;

namespace Services.IntegrationTests;

public class ResilientDistributedCacheTests
{
    private sealed class BrokenCache : IDistributedCache
    {
        public int Calls;

        private Exception Fail()
        {
            Calls++;

            return new InvalidOperationException("Redis is down.");
        }

        public byte[]? Get(string key) => throw Fail();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Fail();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Fail();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Fail();

        public void Refresh(string key) => throw Fail();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw Fail();

        public void Remove(string key) => throw Fail();

        public Task RemoveAsync(string key, CancellationToken token = default) => throw Fail();
    }

    [Fact]
    public async Task Cache_failures_read_as_misses_and_do_not_throw()
    {
        var cache = new ResilientDistributedCache(new BrokenCache(), NullLogger<ResilientDistributedCache>.Instance);

        Assert.Null(await cache.GetAsync("key"));

        await cache.SetAsync("key", [1], new DistributedCacheEntryOptions());
        await cache.RemoveAsync("key");
    }

    [Fact]
    public async Task After_a_failure_the_cache_is_skipped_for_a_while()
    {
        var broken = new BrokenCache();

        var cache = new ResilientDistributedCache(broken, NullLogger<ResilientDistributedCache>.Instance);

        await cache.GetAsync("a");
        await cache.GetAsync("b");
        await cache.SetAsync("c", [1], new DistributedCacheEntryOptions());

        Assert.Equal(1, broken.Calls);
    }

    [Fact]
    public async Task A_healthy_cache_passes_through()
    {
        var inner = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));

        var cache = new ResilientDistributedCache(inner, NullLogger<ResilientDistributedCache>.Instance);

        await cache.SetAsync("key", [7], new DistributedCacheEntryOptions());

        Assert.Equal([7], await cache.GetAsync("key"));
    }
}
