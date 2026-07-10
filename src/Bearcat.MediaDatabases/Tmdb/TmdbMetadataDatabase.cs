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

    public async Task<MediaMetadata?> GetByImdbIdAsync(
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

        return lookup.MediaKind switch
        {
            MediaKind.Movie => MapMovie(response?.MovieResults?.FirstOrDefault()),
            MediaKind.TvSeries => MapTv(response?.TvResults?.FirstOrDefault()),
            MediaKind.TvEpisode => MapEpisode(response?.TvEpisodeResults?.FirstOrDefault()),
            _ => null,
        };
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

            return MapMovie(response?.Results?.FirstOrDefault());
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

            return MapTv(response?.Results?.FirstOrDefault());
        }

        return null;
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
            Genre: null,
            CoverUrl: coverUrl,
            DatabaseUrl: $"https://www.themoviedb.org/movie/{movie.Id}"
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
            Genre: null,
            CoverUrl: coverUrl,
            DatabaseUrl: $"https://www.themoviedb.org/tv/{series.Id}"
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
            DatabaseUrl: $"https://www.themoviedb.org/tv/{episode.ShowId}/season/{episode.SeasonNumber}/episode/{episode.EpisodeNumber}"
        );
    }
}
