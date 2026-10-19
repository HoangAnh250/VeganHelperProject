using Microsoft.Extensions.Caching.Memory;

namespace VeganHelper.BLL.Services;

// Dedicated bounded cache: only statistical DTOs, never authorization or moderation decisions.
// An entry expires after 15 seconds even if it is read continuously.
public sealed class AdminDashboardCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 128 });

    public async Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> load, CancellationToken ct)
        where T : class
    {
        ct.ThrowIfCancellationRequested();
        if (cache.TryGetValue(key, out T? result) && result is not null) return result;
        var value = await load(ct);
        ct.ThrowIfCancellationRequested();
        cache.Set(key, value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) });
        return value;
    }

    public void Dispose() => cache.Dispose();
}
