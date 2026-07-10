using System.Text.RegularExpressions;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ResolveMediaMetadata;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public partial class ReleaseCollectionInfoResolutionService(
    IReleaseCollectionInfoRepository repository,
    MediaMetadataResolver metadataResolver,
    ILogger<ReleaseCollectionInfoResolutionService> logger,
    TimeProvider timeProvider
)
{
    private const int MissingCollectionBatchSize = 50;
    private readonly TimeSpan lastCheckedThreshold = TimeSpan.FromDays(7);

    public async Task<int> ProcessMissingCollectionMetadataAsync(
        CancellationToken cancellationToken = default
    )
    {
        var seenCollectionIds = new HashSet<int>();
        var totalResolvedCount = 0;
        var hasCollectionsWithoutMetadata = true;

        while (hasCollectionsWithoutMetadata)
        {
            var (foundCollections, resolvedCount) = await ResolveBatchAsync(
                seenCollectionIds: seenCollectionIds,
                cancellationToken: cancellationToken
            );

            hasCollectionsWithoutMetadata = foundCollections;
            totalResolvedCount += resolvedCount;
        }

        return totalResolvedCount;
    }

    public async Task<bool> ResolveAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await repository.GetByIdForResolutionAsync(
            releaseCollectionId,
            cancellationToken
        );

        if (collection is null)
        {
            return false;
        }

        if (collection.Metadata is not null)
        {
            return false;
        }

        var resolved = await TryResolveAsync(collection, cancellationToken);
        collection.MetadataCheckedAt = timeProvider.GetLocalNow();
        await SaveChangesSafelyAsync(collection, cancellationToken);

        return resolved;
    }

    private async Task<(bool FoundCollections, int ResolvedCount)> ResolveBatchAsync(
        HashSet<int> seenCollectionIds,
        CancellationToken cancellationToken
    )
    {
        var lastCheckedThresholdDate = timeProvider.GetLocalNow() - lastCheckedThreshold;

        var collections = await repository.GetCollectionsWithoutMetadataAsync(
            count: MissingCollectionBatchSize,
            lastCheckedThreshold: lastCheckedThresholdDate,
            excludedCollectionIds: seenCollectionIds,
            cancellationToken: cancellationToken
        );

        var resolvedCount = 0;

        foreach (var collection in collections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            seenCollectionIds.Add(collection.Id);

            var resolved = await TryResolveAsync(collection, cancellationToken);
            collection.MetadataCheckedAt = timeProvider.GetLocalNow();

            if (resolved)
            {
                resolvedCount++;
            }

            await SaveChangesSafelyAsync(collection, cancellationToken);
        }

        return (FoundCollections: collections.Count > 0, ResolvedCount: resolvedCount);
    }

    private async Task<bool> TryResolveAsync(
        ReleaseCollection collection,
        CancellationToken cancellationToken
    )
    {
        if (collection.Metadata is not null)
        {
            return false;
        }

        var yearMatch = YearRegex().Match(collection.Name);
        var lookup = new MediaMetadataLookup(
            MediaKind: MediaKind.TvSeries,
            ImdbId: ExtractImdbId(collection),
            Title: ExtractSeriesTitle(collection.Name),
            Year: yearMatch.Success ? int.Parse(yearMatch.Value) : null,
            SeasonNumber: null,
            EpisodeNumber: null,
            LanguageCode: collection.PrimaryLanguageCode
        );

        var resolved = await metadataResolver.ResolveAsync(lookup, cancellationToken);

        if (resolved is null)
        {
            return false;
        }

        collection.Metadata = new ReleaseCollectionMetadata
        {
            SeriesDatabaseClassName = resolved.DatabaseClassName,
            Title = resolved.Metadata.Title,
            Description = resolved.Metadata.Description,
            CoverUrl = resolved.Metadata.CoverUrl,
            SeriesDatabaseUrl = resolved.Metadata.DatabaseUrl,
        };

        logger.LogInformation(
            "Resolved metadata for release collection {CollectionName} using {MetadataDatabase}",
            collection.Name,
            resolved.DatabaseClassName
        );

        return true;
    }

    private async Task SaveChangesSafelyAsync(
        ReleaseCollection collection,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateMetadataException(exception))
        {
            repository.DetachPendingMetadata(collection);
            logger.LogInformation(
                exception,
                "Metadata for collection {CollectionName} was already resolved by another worker",
                collection.Name
            );
        }
    }

    private static string? ExtractImdbId(ReleaseCollection collection)
    {
        return collection
            .Releases.SelectMany(release => release.ExternalIdentifiers)
            .Where(identifier => identifier.Type == ExternalIdentifierType.Imdb)
            .OrderBy(identifier => identifier.Source)
            .Select(identifier => identifier.Value)
            .FirstOrDefault();
    }

    private static string? ExtractSeriesTitle(string collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            return null;
        }

        var normalized = collectionName.Replace('.', ' ').Replace('_', ' ');

        var titleMarker = TitleMarkerRegex().Match(normalized);

        if (titleMarker.Success)
        {
            normalized = normalized[..titleMarker.Index];
        }

        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsDuplicateMetadataException(DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_ReleaseCollectionMetadata_ReleaseCollectionId",
            };
    }

    [GeneratedRegex(@"\b(?:(?:19|20)\d{2}|S\d{1,2}(?:E\d{1,3})?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TitleMarkerRegex();

    [GeneratedRegex(@"\b(?:19|20)\d{2}\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
