using System.Globalization;
using System.Text.RegularExpressions;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;

public partial class ReleaseMetadataResolver(
    MediaMetadataResolver metadataResolver,
    INfoDatabaseFactory nfoDatabaseFactory,
    ILogger<ReleaseMetadataResolver> logger
)
{
    private HashSet<string>? nfoDatabaseClassNames;

    public bool NeedsResolution(Release release)
    {
        return release.Metadata is null
            || (
                release.Metadata.MetadataDatabaseClassName != ReleaseMetadata.ManualSource
                && (
                    string.IsNullOrWhiteSpace(release.Metadata.CoverUrl)
                    || IsNfoDatabaseMetadata(release.Metadata)
                )
            );
    }

    public async Task<bool> TryResolveAndAttachMetadataAsync(
        Release release,
        CancellationToken cancellationToken
    )
    {
        if (release.Metadata?.MetadataDatabaseClassName == ReleaseMetadata.ManualSource)
        {
            return false;
        }

        var mediaKind = release.ReleaseContentType switch
        {
            ReleaseContentType.Movie => MediaKind.Movie,
            ReleaseContentType.TvShowEpisode => MediaKind.TvEpisode,
            ReleaseContentType.Game => MediaKind.Game,
            _ => (MediaKind?)null,
        };

        if (mediaKind is null)
        {
            return false;
        }

        var normalizedName = release.Name.Replace('.', ' ').Replace('_', ' ');
        var titleMarker = TitleMarkerRegex().Match(normalizedName);

        var title = titleMarker.Success
            ? normalizedName[..titleMarker.Index].Trim()
            : normalizedName.Trim();

        var yearMatch = YearRegex().Match(normalizedName);
        var episodeMatch = EpisodeRegex().Match(normalizedName);

        var externalTitle = release
            .ReleaseInfo?.ExternalInfos.Select(info => info.Title)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var imdbId = ReleaseExternalIdentifierService.FindValueWithHighestSourcePriority(
            release.ExternalIdentifiers,
            ExternalIdentifierType.Imdb
        );

        var steamAppId = ReleaseExternalIdentifierService.FindValueWithHighestSourcePriority(
            release.ExternalIdentifiers,
            ExternalIdentifierType.Steam
        );

        var manualTmdbId = release
            .ExternalIdentifiers.FirstOrDefault(identifier =>
                identifier.Type == ExternalIdentifierType.Tmdb
                && identifier.Source == ExternalIdentifierSource.Manual
            )
            ?.Value;

        var resolved = await metadataResolver.ResolveAsync(
            new MediaMetadataLookup(
                MediaKind: mediaKind.Value,
                ImdbId: imdbId,
                SteamAppId: steamAppId,
                TmdbId: manualTmdbId,
                TvdbIdOrSlug: null,
                Title: externalTitle ?? title,
                Year: yearMatch.Success
                    ? int.Parse(yearMatch.Value, CultureInfo.InvariantCulture)
                    : null,
                SeasonNumber: episodeMatch.Success
                    ? int.Parse(episodeMatch.Groups["season"].Value, CultureInfo.InvariantCulture)
                    : null,
                EpisodeNumber: episodeMatch.Success
                    ? int.Parse(episodeMatch.Groups["episode"].Value, CultureInfo.InvariantCulture)
                    : null,
                LanguageCode: release.PrimaryLanguageCode
            ),
            cancellationToken
        );

        if (resolved is null)
        {
            return false;
        }

        var existingMetadata = release.Metadata;
        release.Metadata ??= new ReleaseMetadata();
        release.Metadata.MetadataDatabaseClassName = resolved.DatabaseClassName;
        release.Metadata.Title = resolved.Metadata.Title;

        release.Metadata.Genre = string.IsNullOrWhiteSpace(resolved.Metadata.Genre)
            ? existingMetadata?.Genre
            : resolved.Metadata.Genre;

        release.Metadata.Description = string.IsNullOrWhiteSpace(resolved.Metadata.Description)
            ? existingMetadata?.Description
            : resolved.Metadata.Description;

        release.Metadata.CoverUrl = string.IsNullOrWhiteSpace(resolved.Metadata.CoverUrl)
            ? existingMetadata?.CoverUrl
            : resolved.Metadata.CoverUrl;

        release.Metadata.MetadataDatabaseUrl = string.IsNullOrWhiteSpace(
            resolved.Metadata.DatabaseUrl
        )
            ? existingMetadata?.MetadataDatabaseUrl
            : resolved.Metadata.DatabaseUrl;

        StoreIdentifiersReportedByMediaDatabase(release, resolved.Metadata);

        logger.LogInformation(
            "Resolved metadata for release {ReleaseName} using {MetadataDatabase}",
            release.Name,
            resolved.DatabaseClassName
        );

        return true;
    }

    private static void StoreIdentifiersReportedByMediaDatabase(
        Release release,
        MediaMetadata metadata
    )
    {
        if (metadata.TmdbId is not null)
        {
            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Tmdb,
                source: ExternalIdentifierSource.Tmdb,
                value: metadata.TmdbId
            );

            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Imdb,
                source: ExternalIdentifierSource.Tmdb,
                value: metadata.ImdbId
            );
        }

        if (metadata.TvdbId is not null)
        {
            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Tvdb,
                source: ExternalIdentifierSource.Tvdb,
                value: metadata.TvdbId
            );

            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Imdb,
                source: ExternalIdentifierSource.Tvdb,
                value: metadata.ImdbId
            );
        }
    }

    private bool IsNfoDatabaseMetadata(ReleaseMetadata metadata)
    {
        nfoDatabaseClassNames ??= nfoDatabaseFactory
            .GetByClassName()
            .Keys.ToHashSet(StringComparer.Ordinal);

        return nfoDatabaseClassNames.Contains(metadata.MetadataDatabaseClassName);
    }

    [GeneratedRegex(@"\b(?:19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\bS(?<season>\d{1,2})E(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpisodeRegex();

    [GeneratedRegex(@"\b(?:(?:19|20)\d{2}|S\d{1,2}E\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TitleMarkerRegex();
}
