using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.MetadataMatchCorrection;

public class ReleaseCollectionMetadataMatchCorrectionService(
    IReleaseCollectionInfoRepository repository,
    MediaMetadataResolver mediaMetadataResolver,
    ReleaseCollectionInfoResolutionService releaseCollectionInfoResolutionService,
    ReleaseCollectionService releaseCollectionService,
    TimeProvider timeProvider
)
{
    public async Task<MetadataMatchCorrectionPreviewResult> LoadPreviewAsync(
        int releaseCollectionId,
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

        if (!IsReferenceSupportedForSeries(reference))
        {
            return new MetadataMatchCorrectionPreviewResult(
                Failure: MetadataMatchCorrectionFailure.ReferenceNotSupportedForContentType,
                Preview: null
            );
        }

        var collection = await repository.GetByIdForMetadataCorrectionAsync(
            releaseCollectionId,
            cancellationToken
        );

        if (collection.Releases.Count == 0)
        {
            return new MetadataMatchCorrectionPreviewResult(
                Failure: MetadataMatchCorrectionFailure.CollectionHasNoReleases,
                Preview: null
            );
        }

        var resolved = await mediaMetadataResolver.ResolveAsync(
            new MediaMetadataLookup(
                MediaKind: MediaKind.TvSeries,
                ImdbId: reference.GetValueWhenType(ExternalIdentifierType.Imdb),
                SteamAppId: null,
                TmdbId: reference.GetValueWhenType(ExternalIdentifierType.Tmdb),
                TvdbIdOrSlug: reference.GetValueWhenType(ExternalIdentifierType.Tvdb),
                Title: null,
                Year: null,
                SeasonNumber: null,
                EpisodeNumber: null,
                LanguageCode: collection.PrimaryLanguageCode
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
        int releaseCollectionId,
        MetadataMatchCorrectionPreview preview,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await repository.GetByIdForMetadataCorrectionAsync(
            releaseCollectionId,
            cancellationToken
        );

        var previousCoverUrl = collection.Metadata?.CoverUrl;

        collection.Metadata ??= new ReleaseCollectionMetadata();
        collection.Metadata.MetadataDatabaseClassName = preview.DatabaseClassName;
        collection.Metadata.Title = preview.Metadata.Title;
        collection.Metadata.Description = preview.Metadata.Description;
        collection.Metadata.CoverUrl = preview.Metadata.CoverUrl;
        collection.Metadata.MetadataDatabaseUrl = preview.Metadata.DatabaseUrl;

        var canonicalReferenceValue = preview.GetCanonicalReferenceValue();

        foreach (var release in collection.Releases)
        {
            RemoveManualMediaDatabaseIdentifiers(release);

            ReleaseExternalIdentifierService.SyncIdentifierValue(
                release: release,
                type: preview.Reference.Type,
                source: ExternalIdentifierSource.Manual,
                value: canonicalReferenceValue
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
        }

        releaseCollectionService.RemoveCompletedImageUploadsWhenCoverUrlChanged(
            collection,
            previousCoverUrl
        );

        collection.MetadataCheckedAt = timeProvider.GetLocalNow();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveCorrectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await repository.GetByIdForMetadataCorrectionAsync(
            releaseCollectionId,
            cancellationToken
        );

        var previousCoverUrl = collection.Metadata?.CoverUrl;

        foreach (var release in collection.Releases)
        {
            RemoveManualMediaDatabaseIdentifiers(release);
        }

        if (collection.Metadata is not null)
        {
            repository.Remove(collection.Metadata);
            collection.Metadata = null;
        }

        await releaseCollectionInfoResolutionService.TryResolveAsync(
            collection: collection,
            replaceExisting: true,
            cancellationToken: cancellationToken
        );

        releaseCollectionService.RemoveCompletedImageUploadsWhenCoverUrlChanged(
            collection,
            previousCoverUrl
        );

        collection.MetadataCheckedAt = timeProvider.GetLocalNow();

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static bool IsReferenceSupportedForSeries(MediaDatabaseReference reference)
    {
        return reference.Type switch
        {
            ExternalIdentifierType.Imdb => true,
            ExternalIdentifierType.Tmdb => reference.Value.StartsWith(
                "tv/",
                StringComparison.Ordinal
            ),
            ExternalIdentifierType.Tvdb => true,
            _ => false,
        };
    }

    private static void RemoveManualMediaDatabaseIdentifiers(Release release)
    {
        release.ExternalIdentifiers.RemoveAll(identifier =>
            identifier.Source == ExternalIdentifierSource.Manual
            && identifier.Type
                is ExternalIdentifierType.Imdb
                    or ExternalIdentifierType.Tmdb
                    or ExternalIdentifierType.Tvdb
        );
    }
}
