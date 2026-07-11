using Refit;

namespace Bearcat.MediaDatabases.Tmdb.Api;

public interface ITmdbApi
{
    [Get("/3/find/{externalId}")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TmdbFindResponse>> FindAsync(
        string externalId,
        [AliasAs("api_key")] string apiKey,
        [AliasAs("external_source")] string externalSource,
        [AliasAs("language")] string? language,
        CancellationToken cancellationToken = default
    );

    [Get("/3/search/movie")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TmdbSearchResponse<TmdbMovieResponse>>> SearchMoviesAsync(
        [AliasAs("api_key")] string apiKey,
        [AliasAs("query")] string query,
        [AliasAs("language")] string? language,
        [AliasAs("primary_release_year")] int? year,
        [AliasAs("include_adult")] bool includeAdult,
        CancellationToken cancellationToken = default
    );

    [Get("/3/search/tv")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TmdbSearchResponse<TmdbTvResponse>>> SearchTvAsync(
        [AliasAs("api_key")] string apiKey,
        [AliasAs("query")] string query,
        [AliasAs("language")] string? language,
        [AliasAs("first_air_date_year")] int? year,
        [AliasAs("include_adult")] bool includeAdult,
        CancellationToken cancellationToken = default
    );

    [Get("/3/authentication")]
    [Headers("User-Agent: Bearcat/1.0")]
    Task<ApiResponse<TmdbAuthenticationResponse>> CheckAuthenticationAsync(
        [AliasAs("api_key")] string apiKey,
        CancellationToken cancellationToken = default
    );
}
