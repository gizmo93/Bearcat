using System.Text.Json.Serialization;

namespace Bearcat.SeriesDatabases.Tmdb.Api;

public record TmdbFindResponse(
    [property: JsonPropertyName("movie_results")] IReadOnlyList<TmdbMovieResponse>? MovieResults,
    [property: JsonPropertyName("tv_results")] IReadOnlyList<TmdbTvResponse>? TvResults,
    [property: JsonPropertyName("tv_episode_results")]
        IReadOnlyList<TmdbTvEpisodeResponse>? TvEpisodeResults
);

public record TmdbSearchResponse<T>(IReadOnlyList<T>? Results);

public record TmdbMovieResponse(
    long Id,
    string? Title,
    string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath
);

public record TmdbTvResponse(
    long Id,
    string? Name,
    string? Overview,
    [property: JsonPropertyName("poster_path")] string? PosterPath
);

public record TmdbTvEpisodeResponse(
    long Id,
    string? Name,
    string? Overview,
    [property: JsonPropertyName("still_path")] string? StillPath,
    [property: JsonPropertyName("show_id")] long ShowId,
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("episode_number")] int EpisodeNumber
);

public record TmdbAuthenticationResponse(bool Success);
