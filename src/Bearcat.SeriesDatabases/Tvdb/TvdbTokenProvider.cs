using System.Collections.Concurrent;
using System.Net;
using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.SeriesDatabases.Tvdb.Api;
using Refit;

namespace Bearcat.SeriesDatabases.Tvdb;

/// <summary>
/// Caches bearer tokens per API key. TheTVDB tokens are valid for roughly a month, so we keep
/// them in memory and only re-login when a token is missing or the API rejects it.
/// </summary>
public class TvdbTokenProvider(ITvdbApi api)
{
    private readonly ConcurrentDictionary<string, string> tokensByCacheKey = new();
    private readonly SemaphoreSlim loginGate = new(1, 1);

    public async Task<string> GetTokenAsync(TvdbConfig config, CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(config);

        if (tokensByCacheKey.TryGetValue(cacheKey, out var cachedToken))
        {
            return cachedToken;
        }

        await loginGate.WaitAsync(cancellationToken);

        try
        {
            if (tokensByCacheKey.TryGetValue(cacheKey, out cachedToken))
            {
                return cachedToken;
            }

            var token = await LoginAsync(config, cancellationToken);
            tokensByCacheKey[cacheKey] = token;
            return token;
        }
        finally
        {
            loginGate.Release();
        }
    }

    public void Invalidate(TvdbConfig config)
    {
        tokensByCacheKey.TryRemove(GetCacheKey(config), out _);
    }

    private async Task<string> LoginAsync(TvdbConfig config, CancellationToken cancellationToken)
    {
        var response = await api.LoginAsync(new TvdbLoginRequest(config.ApiKey), cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new SeriesDatabaseRateLimitExceededException("TheTVDB", resetAt: null);
        }

        if (
            !response.IsSuccessStatusCode
            || string.IsNullOrWhiteSpace(response.Content?.Data?.Token)
        )
        {
            if (response.Error is not null)
            {
                throw response.Error;
            }

            throw new HttpRequestException(
                $"TheTVDB login failed with status code {response.StatusCode}."
            );
        }

        return response.Content.Data.Token;
    }

    private static string GetCacheKey(TvdbConfig config)
    {
        return config.ApiKey;
    }
}
