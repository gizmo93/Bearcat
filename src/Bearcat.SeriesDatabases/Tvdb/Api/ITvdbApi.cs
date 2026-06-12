using Refit;

namespace Bearcat.SeriesDatabases.Tvdb.Api;

public interface ITvdbApi
{
    [Post("/v4/login")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TvdbResponse<TvdbLoginData>>> LoginAsync(
        [Body] TvdbLoginRequest request,
        CancellationToken cancellationToken = default
    );

    [Get("/v4/search/remoteid/{remoteId}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TvdbResponse<IReadOnlyList<TvdbRemoteIdResult>>>> SearchByRemoteIdAsync(
        string remoteId,
        [Authorize("Bearer")] string token,
        CancellationToken cancellationToken = default
    );

    [Get("/v4/search")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TvdbResponse<IReadOnlyList<TvdbSearchResult>>>> SearchAsync(
        [AliasAs("query")] string query,
        [AliasAs("type")] string type,
        [AliasAs("limit")] int limit,
        [Authorize("Bearer")] string token,
        CancellationToken cancellationToken = default
    );

    [Get("/v4/series/{id}/translations/{language}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TvdbResponse<TvdbTranslation>>> GetSeriesTranslationAsync(
        long id,
        string language,
        [Authorize("Bearer")] string token,
        CancellationToken cancellationToken = default
    );
}
