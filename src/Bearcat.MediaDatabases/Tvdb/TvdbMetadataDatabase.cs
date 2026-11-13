using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Extensions;

namespace Bearcat.MediaDatabases.Tvdb;

public partial class TvdbMetadataDatabase(TvdbClient client) : IMediaMetadataDatabase
{
    public string Name => "TheTVDB";

    public int ResolutionPriority => 0;

    public IReadOnlyList<MediaKind> SupportedMediaKinds => [MediaKind.TvSeries];

    public IReadOnlyList<string> ConfigurationKeys => [TvdbConfig.ApiKeyConfigKey];

    public async Task<MediaMetadata?> GetByExternalIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (lookup.MediaKind != MediaKind.TvSeries || string.IsNullOrWhiteSpace(lookup.ImdbId))
        {
            return null;
        }

        var tvdbConfig = config.As<TvdbConfig>();

        var series = await client.GetSeriesByImdbIdAsync(
            config: tvdbConfig,
            imdbId: lookup.ImdbId,
            cancellationToken: cancellationToken
        );

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
            imdbId: null,
            languageCode: lookup.LanguageCode,
            cancellationToken: cancellationToken
        );
    }

    public async Task<MediaMetadata?> GetByTitleAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (lookup.MediaKind != MediaKind.TvSeries || string.IsNullOrWhiteSpace(lookup.Title))
        {
            return null;
        }

        var tvdbConfig = config.As<TvdbConfig>();

        var result = await client.SearchSeriesByTitleAsync(
            config: tvdbConfig,
            title: lookup.Title,
            cancellationToken: cancellationToken
        );

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
            imdbId: null,
            languageCode: lookup.LanguageCode,
            cancellationToken: cancellationToken
        );
    }

    public async Task<MediaMetadata?> GetByDatabaseIdAsync(
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        if (
            lookup.MediaKind != MediaKind.TvSeries
            || string.IsNullOrWhiteSpace(lookup.TvdbIdOrSlug)
        )
        {
            return null;
        }

        var tvdbConfig = config.As<TvdbConfig>();

        if (
            !long.TryParse(
                lookup.TvdbIdOrSlug,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var seriesId
            )
        )
        {
            var seriesBySlug = await client.GetSeriesBySlugAsync(
                config: tvdbConfig,
                slug: lookup.TvdbIdOrSlug,
                cancellationToken: cancellationToken
            );

            if (seriesBySlug is null)
            {
                return null;
            }

            seriesId = seriesBySlug.Id;
        }

        var series = await client.GetSeriesExtendedAsync(
            config: tvdbConfig,
            seriesId: seriesId,
            cancellationToken: cancellationToken
        );

        if (series is null)
        {
            return null;
        }

        var imdbId = series
            .RemoteIds?.Select(remoteId => remoteId.Id)
            .FirstOrDefault(remoteId => remoteId is not null && ImdbIdRegex().IsMatch(remoteId));

        return await BuildSeriesInfoAsync(
            config: tvdbConfig,
            seriesId: series.Id,
            fallbackName: series.Name,
            fallbackOverview: series.Overview,
            image: series.Image,
            slug: series.Slug,
            imdbId: imdbId,
            languageCode: lookup.LanguageCode,
            cancellationToken: cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IMediaMetadataDatabaseConfig config,
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

    public IMediaMetadataDatabaseConfig DeserializeConfig(string serializedConfig)
    {
        var dictionary =
            JsonSerializer.Deserialize<Dictionary<string, string>>(serializedConfig) ?? [];

        return new TvdbConfig(
            dictionary.GetValueOrDefault(TvdbConfig.ApiKeyConfigKey, string.Empty)
        );
    }

    private async Task<MediaMetadata?> BuildSeriesInfoAsync(
        TvdbConfig config,
        long seriesId,
        string? fallbackName,
        string? fallbackOverview,
        string? image,
        string? slug,
        string? imdbId,
        string? languageCode,
        CancellationToken cancellationToken
    )
    {
        var translation = string.IsNullOrWhiteSpace(languageCode)
            ? null
            : await client.GetTranslationAsync(
                config: config,
                seriesId: seriesId,
                languageCode: CultureInfo.GetCultureInfo(languageCode).ThreeLetterISOLanguageName,
                cancellationToken: cancellationToken
            );

        var title = FirstNonEmpty(translation?.Name, fallbackName);

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new MediaMetadata(
            Title: title,
            Description: FirstNonEmpty(translation?.Overview, fallbackOverview),
            Genre: null,
            CoverUrl: NullIfWhiteSpace(image),
            DatabaseUrl: string.IsNullOrWhiteSpace(slug)
                ? null
                : $"https://www.thetvdb.com/series/{slug}",
            ImdbId: imdbId,
            TmdbId: null,
            TvdbId: seriesId.ToString(CultureInfo.InvariantCulture)
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

    [GeneratedRegex(@"^tt\d{7,8}$")]
    private static partial Regex ImdbIdRegex();
}
