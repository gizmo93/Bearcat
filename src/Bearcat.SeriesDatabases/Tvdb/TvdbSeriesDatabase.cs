using System.Text.Json;
using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.SeriesDatabases.Extensions;

namespace Bearcat.SeriesDatabases.Tvdb;

public class TvdbSeriesDatabase(TvdbClient client) : ISeriesDatabase
{
    public string Name => "TheTVDB";

    public int ResolutionPriority => 0;

    public IReadOnlyList<string> ConfigurationKeys => [TvdbConfig.ApiKeyConfigKey];

    public async Task<SeriesInfo?> GetSeriesInfoByImdbIdAsync(
        ISeriesDatabaseConfig config,
        string imdbId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return null;
        }

        var tvdbConfig = config.As<TvdbConfig>();
        var series = await client.GetSeriesByImdbIdAsync(tvdbConfig, imdbId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        return await BuildSeriesInfoAsync(
            config: tvdbConfig,
            seriesId: series.Id,
            fallbackName: series.Name,
            fallbackOverview: series.Overview,
            image: series.Image,
            slug: series.Slug,
            cancellationToken: cancellationToken
        );
    }

    public async Task<SeriesInfo?> GetSeriesInfoByTitleAsync(
        ISeriesDatabaseConfig config,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var tvdbConfig = config.As<TvdbConfig>();
        var result = await client.SearchSeriesByTitleAsync(tvdbConfig, title, cancellationToken);

        if (result?.TvdbId is null || !long.TryParse(result.TvdbId, out var seriesId))
        {
            return null;
        }

        return await BuildSeriesInfoAsync(
            config: tvdbConfig,
            seriesId: seriesId,
            fallbackName: result.Name,
            fallbackOverview: result.Overview,
            image: result.ImageUrl,
            slug: result.Slug,
            cancellationToken: cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        ISeriesDatabaseConfig config,
        CancellationToken cancellationToken = default
    )
    {
        var tvdbConfig = config.As<TvdbConfig>();

        try
        {
            await client.ValidateLoginAsync(tvdbConfig, cancellationToken);
            return new TryLoginResult(IsSuccess: true, ErrorMessage: null);
        }
        catch (Exception exception)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: exception.Message);
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        var dictionary = new Dictionary<string, string>();

        if (config.TryGetValue(TvdbConfig.ApiKeyConfigKey, out var apiKey))
        {
            dictionary[TvdbConfig.ApiKeyConfigKey] = apiKey;
        }

        return JsonSerializer.Serialize(dictionary);
    }

    public ISeriesDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        var dictionary =
            JsonSerializer.Deserialize<Dictionary<string, string>>(serializedConfig) ?? [];

        return new TvdbConfig(
            dictionary.GetValueOrDefault(TvdbConfig.ApiKeyConfigKey, string.Empty)
        );
    }

    private async Task<SeriesInfo?> BuildSeriesInfoAsync(
        TvdbConfig config,
        long seriesId,
        string? fallbackName,
        string? fallbackOverview,
        string? image,
        string? slug,
        CancellationToken cancellationToken
    )
    {
        var translation = await client.GetGermanTranslationAsync(
            config,
            seriesId,
            cancellationToken
        );

        var title = FirstNonEmpty(translation?.Name, fallbackName);

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new SeriesInfo(
            Title: title,
            Description: FirstNonEmpty(translation?.Overview, fallbackOverview),
            CoverUrl: NullIfWhiteSpace(image),
            SeriesDatabaseUrl: string.IsNullOrWhiteSpace(slug)
                ? null
                : $"https://www.thetvdb.com/series/{slug}"
        );
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? null : second.Trim();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
