using Microsoft.Extensions.Caching.Memory;

namespace VeganHelper.API.Authorization;

public sealed class LoginFailureLimiter : IDisposable
{
    private readonly MemoryCache attempts = new(new MemoryCacheOptions { SizeLimit = 100_000 });
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1)).ToArray();

    public async Task<T> RunAsync<T>(string identifier, string address, Func<Task<T>> login,
        Func<T, bool> failed, Func<T, bool> succeeded, Func<int, T> throttled, CancellationToken ct)
    {
        var key = (identifier.Trim().ToUpperInvariant(), address);
        var gate = gates[(int)((uint)key.GetHashCode() % gates.Length)];
        await gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            attempts.TryGetValue<(int Count, DateTimeOffset Expires)>(key, out var previous);
            if (previous.Count >= 5 && previous.Expires > now)
                return throttled((int)Math.Ceiling((previous.Expires - now).TotalSeconds));
            var result = await login();
            if (failed(result))
            {
                var expires = previous.Expires > now ? previous.Expires : now.AddMinutes(15);
                attempts.Set(key, (previous.Expires > now ? previous.Count + 1 : 1, expires),
                    new MemoryCacheEntryOptions { AbsoluteExpiration = expires, Size = 1 });
            }
            else if (succeeded(result)) attempts.Remove(key);
            return result;
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        attempts.Dispose();
        foreach (var gate in gates) gate.Dispose();
    }
}
