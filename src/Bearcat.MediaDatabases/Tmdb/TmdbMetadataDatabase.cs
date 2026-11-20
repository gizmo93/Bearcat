using System.Globalization;
using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Extensions;
using Bearcat.MediaDatabases.Tmdb.Api;
using Refit;

namespace Bearcat.MediaDatabases.Tmdb;

public class TmdbMetadataDatabase(ITmdbApi api) : IMediaMetadataDatabase
{
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    public string Name => "The Movie Database";

    public int ResolutionPriority => 100;

    public IReadOnlyList<MediaKind> SupportedMediaKinds =>
        [MediaKind.Movie, MediaKind.TvSeries, MediaKind.TvEpisode];

    public IReadOnlyList<string> ConfigurationKeys => [TmdbConfig.ApiKeyConfigKey];

    public async Task<MediaMetadata?> GetByExternalIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(lookup.ImdbId))
        {
            return null;
        }

        var tmdbConfig = config.As<TmdbConfig>();
        var response = await SendAsync(
            api.FindAsync(
                externalId: lookup.ImdbId,
                apiKey: tmdbConfig.ApiKey,
                externalSource: "imdb_id",
                language: lookup.LanguageCode,
                cancellationToken: cancellationToken
            )
        );

        if (lookup.MediaKind == MediaKind.Movie)
        {
            return await GetMovieFromSearchResultAsync(
                searchResult: response?.MovieResults?.FirstOrDefault(),
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        if (lookup.MediaKind == MediaKind.TvSeries)
        {
            return await GetTvFromSearchResultAsync(
                searchResult: response?.TvResults?.FirstOrDefault(),
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        if (lookup.MediaKind == MediaKind.TvEpisode)
        {
            var episode = MapEpisode(response?.TvEpisodeResults?.FirstOrDefault());

            if (episode is not null)
            {
                return episode;
            }

            return await GetTvFromSearchResultAsync(
                searchResult: response?.TvResults?.FirstOrDefault(),
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        return null;
    }

    public async Task<MediaMetadata?> GetByTitleAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(lookup.Title))
        {
            return null;
        }

        var tmdbConfig = config.As<TmdbConfig>();

        if (lookup.MediaKind == MediaKind.Movie)
        {
            var response = await SendAsync(
                api.SearchMoviesAsync(
                    apiKey: tmdbConfig.ApiKey,
                    query: lookup.Title,
                    language: lookup.LanguageCode,
                    year: lookup.Year,
                    includeAdult: false,
                    cancellationToken: cancellationToken
                )
            );

            return await GetMovieFromSearchResultAsync(
                searchResult: response?.Results?.FirstOrDefault(),
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        if (lookup.MediaKind is MediaKind.TvSeries or MediaKind.TvEpisode)
        {
            var response = await SendAsync(
                api.SearchTvAsync(
                    apiKey: tmdbConfig.ApiKey,
                    query: lookup.Title,
                    language: lookup.LanguageCode,
                    year: lookup.Year,
                    includeAdult: false,
                    cancellationToken: cancellationToken
                )
            );

            return await GetTvFromSearchResultAsync(
                searchResult: response?.Results?.FirstOrDefault(),
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        return null;
    }

    public async Task<MediaMetadata?> GetByDatabaseIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(lookup.TmdbId))
        {
            return null;
        }

        var tmdbConfig = config.As<TmdbConfig>();
        var tmdbIdParts = lookup.TmdbId.Split('/');
        var id = long.Parse(tmdbIdParts[1], CultureInfo.InvariantCulture);

        if (tmdbIdParts[0] == "movie")
        {
            return await GetMovieByIdAsync(
                movieId: id,
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        if (tmdbIdParts[0] == "tv")
        {
            return await GetTvByIdAsync(
                seriesId: id,
                config: tmdbConfig,
                languageCode: lookup.LanguageCode,
                cancellationToken: cancellationToken
            );
        }

        throw new ArgumentOutOfRangeException(nameof(lookup), lookup.TmdbId, null);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IMediaMetadataDatabaseConfig config,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var tmdbConfig = config.As<TmdbConfig>();
            var response = await api.CheckAuthenticationAsync(tmdbConfig.ApiKey, cancellationToken);

            return response.IsSuccessStatusCode && response.Content?.Success == true
                ? new TryLoginResult(true, null)
                : new TryLoginResult(false, response.Error?.Message ?? "Authentication failed.");
        }
        catch (Exception exception)
        {
            return new TryLoginResult(false, exception.Message);
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        var apiKey = config.GetValueOrDefault(TmdbConfig.ApiKeyConfigKey) ?? string.Empty;
        return JsonSerializer.Serialize(
            new Dictionary<string, string> { [TmdbConfig.ApiKeyConfigKey] = apiKey }
        );
    }

    public IMediaMetadataDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(serializedConfig) ?? [];
        return new TmdbConfig(config.GetValueOrDefault(TmdbConfig.ApiKeyConfigKey) ?? string.Empty);
    }

    private async Task<MediaMetadata?> GetMovieFromSearchResultAsync(
        TmdbMovieResponse? searchResult,
        TmdbConfig config,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        if (searchResult is null)
        {
            return null;
        }

        return await GetMovieByIdAsync(
            movieId: searchResult.Id,
            config: config,
            languageCode: languageCode,
            cancellationToken: cancellationToken
        );
    }

    private async Task<MediaMetadata?> GetTvFromSearchResultAsync(
        TmdbTvResponse? searchResult,
        TmdbConfig config,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        if (searchResult is null)
        {
            return null;
        }

        return await GetTvByIdAsync(
            seriesId: searchResult.Id,
            config: config,
            languageCode: languageCode,
            cancellationToken: cancellationToken
        );
    }

    private async Task<MediaMetadata?> GetMovieByIdAsync(
        long movieId,
        TmdbConfig config,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        var movie = await SendAsync(
            api.GetMovieAsync(
                movieId: movieId,
                apiKey: config.ApiKey,
                language: languageCode,
                cancellationToken: cancellationToken
            )
        );

        return MapMovie(movie);
    }

    private async Task<MediaMetadata?> GetTvByIdAsync(
        long seriesId,
        TmdbConfig config,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        var series = await SendAsync(
            api.GetTvAsync(
                seriesId: seriesId,
                apiKey: config.ApiKey,
                language: languageCode,
                appendToResponse: "external_ids",
                cancellationToken: cancellationToken
            )
        );

        return MapTv(series);
    }

    private static async Task<T?> SendAsync<T>(Task<ApiResponse<T>> request)
    {
        using var response = await request;

        if (response.IsSuccessStatusCode)
        {
            return response.Content;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new MediaMetadataDatabaseRateLimitExceededException("TMDB", null);
        }

        if (response.Error is not null)
        {
            throw response.Error;
        }

        throw new HttpRequestException($"TMDB returned {response.StatusCode}.");
    }

    private static MediaMetadata? MapMovie(TmdbMovieResponse? movie)
    {
        if (movie is null || string.IsNullOrWhiteSpace(movie.Title))
        {
            return null;
        }

        var coverUrl = string.IsNullOrWhiteSpace(movie.PosterPath)
            ? null
            : $"{ImageBaseUrl}{movie.PosterPath}";

        return new MediaMetadata(
            Title: movie.Title,
            Description: movie.Overview,
            Genre: JoinGenreNames(movie.Genres),
            CoverUrl: coverUrl,
            DatabaseUrl: $"https://www.themoviedb.org/movie/{movie.Id}",
            ImdbId: NullIfWhiteSpace(movie.ImdbId),
            TmdbId: $"movie/{movie.Id}",
            TvdbId: null
        );
    }

    private static MediaMetadata? MapTv(TmdbTvResponse? series)
    {
        if (series is null || string.IsNullOrWhiteSpace(series.Name))
        {
            return null;
        }

        var coverUrl = string.IsNullOrWhiteSpace(series.PosterPath)
            ? null
            : $"{ImageBaseUrl}{series.PosterPath}";

        return new MediaMetadata(
            Title: series.Name,
            Description: series.Overview,
            Genre: JoinGenreNames(series.Genres),
            CoverUrl: coverUrl,
            DatabaseUrl: $"https://www.themoviedb.org/tv/{series.Id}",
            ImdbId: NullIfWhiteSpace(series.ExternalIds?.ImdbId),
            TmdbId: $"tv/{series.Id}",
            TvdbId: null
        );
    }

    private static MediaMetadata? MapEpisode(TmdbTvEpisodeResponse? episode)
    {
        if (episode is null || string.IsNullOrWhiteSpace(episode.Name))
        {
            return null;
        }

        var coverUrl = string.IsNullOrWhiteSpace(episode.StillPath)
            ? null
            : $"{ImageBaseUrl}{episode.StillPath}";

        return new MediaMetadata(
            Title: episode.Name,
            Description: episode.Overview,
            Genre: null,
            CoverUrl: coverUrl,
            DatabaseUrl: $"https://www.themoviedb.org/tv/{episode.ShowId}/season/{episode.SeasonNumber}/episode/{episode.EpisodeNumber}",
            ImdbId: null,
            TmdbId: $"tv/{episode.ShowId}",
            TvdbId: null
        );
    }

    private static string? JoinGenreNames(IReadOnlyList<TmdbGenreResponse>? genres)
    {
        var names = genres
            ?.Select(genre => genre.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        return names is null || names.Count == 0 ? null : string.Join(", ", names);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
