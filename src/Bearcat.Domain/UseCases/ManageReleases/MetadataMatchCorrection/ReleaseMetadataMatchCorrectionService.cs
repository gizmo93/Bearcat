using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;

public class ReleaseMetadataMatchCorrectionService(
    IReleaseInfoRepository repository,
    MediaMetadataResolver mediaMetadataResolver,
    ReleaseMetadataResolver releaseMetadataResolver,
    ReleaseInfoService releaseInfoService,
    TimeProvider timeProvider
)
{
    public async Task<MetadataMatchCorrectionPreviewResult> LoadPreviewAsync(
        int releaseId,
        string? input,
        CancellationToken cancellationToken = default
    )
    {
        var reference = MediaDatabaseReferenceParser.Parse(input);

        if (reference is null)
        {
            return new MetadataMatchCorrectionPreviewResult(
                Failure: MetadataMatchCorrectionFailure.InputNotRecognized,
                Preview: null
            );
        }

        var release = await repository.GetReleaseWithInfoAsync(releaseId, cancellationToken);
        var mediaKind = ReleaseMetadataResolver.GetMediaKind(release.ReleaseContentType);

        if (mediaKind is null || !IsReferenceSupportedForMediaKind(reference, mediaKind.Value))
        {
            return new MetadataMatchCorrectionPreviewResult(
                Failure: MetadataMatchCorrectionFailure.ReferenceNotSupportedForContentType,
                Preview: null
            );
        }

        var resolved = await mediaMetadataResolver.ResolveAsync(
            new MediaMetadataLookup(
                MediaKind: mediaKind.Value,
                ImdbId: GetValueWhenType(reference, ExternalIdentifierType.Imdb),
                SteamAppId: GetValueWhenType(reference, ExternalIdentifierType.Steam),
                TmdbId: GetValueWhenType(reference, ExternalIdentifierType.Tmdb),
                TvdbIdOrSlug: null,
                Title: null,
                Year: null,
                SeasonNumber: null,
                EpisodeNumber: null,
                LanguageCode: release.PrimaryLanguageCode
            ),
            cancellationToken
        );

        if (resolved is null)
        {
            return new MetadataMatchCorrectionPreviewResult(
                Failure: MetadataMatchCorrectionFailure.NotFound,
                Preview: null
            );
        }

        return new MetadataMatchCorrectionPreviewResult(
            Failure: null,
            Preview: new MetadataMatchCorrectionPreview(
                Reference: reference,
                DatabaseClassName: resolved.DatabaseClassName,
                Metadata: resolved.Metadata
            )
        );
    }

    public async Task ApplyAsync(
        int releaseId,
        MetadataMatchCorrectionPreview preview,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseForMetadataUpdateAsync(
            releaseId,
            cancellationToken
        );

        var previousCoverUrl = release.Metadata?.CoverUrl;

        release.Metadata ??= new ReleaseMetadata();
        release.Metadata.MetadataDatabaseClassName = preview.DatabaseClassName;
        release.Metadata.Title = preview.Metadata.Title;
        release.Metadata.Description = preview.Metadata.Description;
        release.Metadata.CoverUrl = preview.Metadata.CoverUrl;
        release.Metadata.MetadataDatabaseUrl = preview.Metadata.DatabaseUrl;

        if (!string.IsNullOrWhiteSpace(preview.Metadata.Genre))
        {
            release.Metadata.Genre = preview.Metadata.Genre;
        }

        RemoveManualIdentifiers(release);

        ReleaseExternalIdentifierService.SyncIdentifierValue(
            release: release,
            type: preview.Reference.Type,
            source: ExternalIdentifierSource.Manual,
            value: GetCanonicalReferenceValue(preview)
        );

        if (preview.Reference.Type != ExternalIdentifierType.Imdb)
        {
            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Imdb,
                source: ExternalIdentifierSource.Manual,
                value: preview.Metadata.ImdbId
            );
        }

        ReleaseExternalIdentifierService.StoreIdentifiersReportedByMediaDatabase(
            release,
            preview.Metadata
        );

        releaseInfoService.RemoveCompletedImageUploadsWhenCoverUrlChanged(
            release,
            previousCoverUrl
        );

        MarkMetadataChecked(release);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCorrectionAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseForMetadataUpdateAsync(
            releaseId,
            cancellationToken
        );

        var previousCoverUrl = release.Metadata?.CoverUrl;

        release.ExternalIdentifiers.RemoveAll(identifier =>
            identifier.Source
                is ExternalIdentifierSource.Manual
                    or ExternalIdentifierSource.Tmdb
                    or ExternalIdentifierSource.Tvdb
        );

        if (release.Metadata is not null)
        {
            repository.Remove(release.Metadata);
            release.Metadata = null;
        }

        await releaseMetadataResolver.TryResolveAndAttachMetadataAsync(release, cancellationToken);

        releaseInfoService.RemoveCompletedImageUploadsWhenCoverUrlChanged(
            release,
            previousCoverUrl
        );

        MarkMetadataChecked(release);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static bool IsReferenceSupportedForMediaKind(
        MediaDatabaseReference reference,
        MediaKind mediaKind
    )
    {
        return (reference.Type, mediaKind) switch
        {
            (ExternalIdentifierType.Imdb, MediaKind.Movie or MediaKind.TvEpisode) => true,
            (ExternalIdentifierType.Tmdb, MediaKind.Movie) => reference.Value.StartsWith(
                "movie/",
                StringComparison.Ordinal
            ),
            (ExternalIdentifierType.Tmdb, MediaKind.TvEpisode) => reference.Value.StartsWith(
                "tv/",
                StringComparison.Ordinal
            ),
            (ExternalIdentifierType.Steam, MediaKind.Game) => true,
            _ => false,
        };
    }

    private static string? GetValueWhenType(
        MediaDatabaseReference reference,
        ExternalIdentifierType type
    )
    {
        return reference.Type == type ? reference.Value : null;
    }

    private static string GetCanonicalReferenceValue(MetadataMatchCorrectionPreview preview)
    {
        return preview.Reference.Type switch
        {
            ExternalIdentifierType.Tmdb => preview.Metadata.TmdbId
                ?? throw new InvalidOperationException(
                    "The media database did not report a TMDB id for a TMDB reference."
                ),
            ExternalIdentifierType.Tvdb => preview.Metadata.TvdbId
                ?? throw new InvalidOperationException(
                    "The media database did not report a TVDB id for a TVDB reference."
                ),
            _ => preview.Reference.Value,
        };
    }

    private static void RemoveManualIdentifiers(Release release)
    {
        release.ExternalIdentifiers.RemoveAll(identifier =>
            identifier.Source == ExternalIdentifierSource.Manual
        );
    }

    private void MarkMetadataChecked(Release release)
    {
        release.MetadataCheckedAt = timeProvider.GetLocalNow();

        if (release.ReleaseCollection is not null)
        {
            release.ReleaseCollection.MetadataCheckedAt = null;
        }
    }
}
