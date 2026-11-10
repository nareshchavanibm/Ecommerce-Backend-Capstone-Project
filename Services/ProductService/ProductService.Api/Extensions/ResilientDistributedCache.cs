using Microsoft.Extensions.Caching.Distributed;

namespace ProductService.Api.Extensions
{
    // The cache is an optimisation: when Redis is down, reads fall through to the database.
    public sealed class ResilientDistributedCache(
        IDistributedCache inner,
        ILogger<ResilientDistributedCache> logger) : IDistributedCache
    {
        private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

        private long _skipUntilTicks;

        public byte[]? Get(string key) => Run(() => inner.Get(key), null);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            RunAsync(() => inner.GetAsync(key, token), null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            Run(() => { inner.Set(key, value, options); return true; }, false);

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            RunAsync(async () => { await inner.SetAsync(key, value, options, token); return true; }, false);

        public void Refresh(string key) =>
            Run(() => { inner.Refresh(key); return true; }, false);

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            RunAsync(async () => { await inner.RefreshAsync(key, token); return true; }, false);

        public void Remove(string key) =>
            Run(() => { inner.Remove(key); return true; }, false);

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            RunAsync(async () => { await inner.RemoveAsync(key, token); return true; }, false);

        private bool IsOpen => Environment.TickCount64 < Interlocked.Read(ref _skipUntilTicks);

        private T Run<T>(Func<T> action, T fallback)
        {
            if (IsOpen)
            {
                return fallback;
            }

            try
            {
                return action();
            }
            catch (Exception ex)
            {
                Trip(ex);

                return fallback;
            }
        }

        private async Task<T> RunAsync<T>(Func<Task<T>> action, T fallback)
        {
            if (IsOpen)
            {
                return fallback;
            }

            try
            {
                return await action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trip(ex);

                return fallback;
            }
        }

        private void Trip(Exception ex)
        {
            Interlocked.Exchange(ref _skipUntilTicks, Environment.TickCount64 + (long)RetryAfter.TotalMilliseconds);

            logger.LogWarning(ex, "Cache unavailable; skipping it for {Seconds}s.", RetryAfter.TotalSeconds);
        }
    }
}
