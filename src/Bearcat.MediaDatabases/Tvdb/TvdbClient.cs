using System.Net;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Tvdb.Api;
using Refit;

namespace Bearcat.MediaDatabases.Tvdb;

public class TvdbClient(ITvdbApi api, TvdbTokenProvider tokenProvider)
{
    public async Task<TvdbSeriesBaseRecord?> GetSeriesByImdbIdAsync(
        TvdbConfig config,
        string imdbId,
        CancellationToken cancellationToken = default
    )
    {
        var results = await SendAsync(
            config: config,
            call: token => api.SearchByRemoteIdAsync(imdbId, token, cancellationToken),
            cancellationToken: cancellationToken
        );

        return results?.FirstOrDefault(result => result.Series is not null)?.Series;
    }

    public async Task<TvdbSearchResult?> SearchSeriesByTitleAsync(
        TvdbConfig config,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        var results = await SendAsync(
            config: config,
            call: token => api.SearchAsync(title, "series", 1, token, cancellationToken),
            cancellationToken: cancellationToken
        );

        return results?.FirstOrDefault();
    }

    public async Task ValidateLoginAsync(
        TvdbConfig config,
        CancellationToken cancellationToken = default
    )
    {
        tokenProvider.Invalidate(config);
        await tokenProvider.GetTokenAsync(config, cancellationToken);
    }

    public async Task<TvdbTranslation?> GetTranslationAsync(
        TvdbConfig config,
        long seriesId,
        string languageCode,
        CancellationToken cancellationToken = default
    )
    {
        return await SendAsync(
            config: config,
            call: token =>
                api.GetSeriesTranslationAsync(seriesId, languageCode, token, cancellationToken),
            cancellationToken: cancellationToken
        );
    }

    private async Task<T?> SendAsync<T>(
        TvdbConfig config,
        Func<string, Task<ApiResponse<TvdbResponse<T>>>> call,
        CancellationToken cancellationToken
    )
    {
        var token = await tokenProvider.GetTokenAsync(config, cancellationToken);
        var response = await call(token);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            tokenProvider.Invalidate(config);
            token = await tokenProvider.GetTokenAsync(config, cancellationToken);
            response = await call(token);
        }

        return HandleResponse(response);
    }

    private static T? HandleResponse<T>(ApiResponse<TvdbResponse<T>> response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return default;
            case HttpStatusCode.TooManyRequests:
                throw new MediaMetadataDatabaseRateLimitExceededException("TheTVDB", resetAt: null);
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.Error is not null)
            {
                throw response.Error;
            }

            throw new HttpRequestException(
                $"TheTVDB request failed with status code {response.StatusCode}."
            );
        }

        return response.Content is { Data: { } data } ? data : default;
    }
}
