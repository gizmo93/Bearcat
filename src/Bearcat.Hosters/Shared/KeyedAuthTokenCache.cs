using System.Collections.Concurrent;

namespace Bearcat.Hosters.Shared;

public sealed class KeyedAuthTokenCache(TimeSpan tokenLifetime)
{
    private readonly record struct CachedToken(string Token, DateTime AuthenticatedAt);

    private readonly ConcurrentDictionary<string, CachedToken> tokensByAccount = new();

    private readonly ConcurrentDictionary<string, SemaphoreSlim> locksByAccount = new();

    public async Task<string> GetOrAuthenticateAsync(
        string accountKey,
        Func<CancellationToken, Task<string>> authenticateAsync,
        CancellationToken cancellationToken
    )
    {
        var accountLock = locksByAccount.GetOrAdd(
            accountKey,
            _ => new SemaphoreSlim(initialCount: 1, maxCount: 1)
        );

        await accountLock.WaitAsync(cancellationToken);

        try
        {
            if (
                tokensByAccount.TryGetValue(accountKey, out var cached)
                && DateTime.UtcNow - cached.AuthenticatedAt < tokenLifetime
            )
            {
                return cached.Token;
            }

            var token = await authenticateAsync(cancellationToken);
            tokensByAccount[accountKey] = new CachedToken(token, DateTime.UtcNow);

            return token;
        }
        finally
        {
            accountLock.Release();
        }
    }
}
