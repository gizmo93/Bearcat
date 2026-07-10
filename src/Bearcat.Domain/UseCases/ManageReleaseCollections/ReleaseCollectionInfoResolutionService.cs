using System.Text.RegularExpressions;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public partial class ReleaseCollectionInfoResolutionService(
    IReleaseCollectionInfoRepository repository,
    IMediaMetadataDatabaseFactory metadataDatabaseFactory,
    ILogger<ReleaseCollectionInfoResolutionService> logger,
    TimeProvider timeProvider
)
{
    private const int MissingCollectionBatchSize = 50;
    private readonly TimeSpan lastCheckedThreshold = TimeSpan.FromDays(7);
    private IReadOnlyList<ActiveSeriesDatabaseRegistrationReadModel>? activeRegistrations;

    public async Task<int> ProcessMissingCollectionMetadataAsync(
        CancellationToken cancellationToken = default
    )
    {
        var registrations = await GetActiveSeriesDatabaseRegistrationsAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            return 0;
        }

        var seenCollectionIds = new HashSet<int>();
        var totalResolvedCount = 0;
        var hasCollectionsWithoutMetadata = true;

        while (hasCollectionsWithoutMetadata)
        {
            var (foundCollections, resolvedCount) = await ResolveBatchAsync(
                registrations: registrations,
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
        var registrations = await GetActiveSeriesDatabaseRegistrationsAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            return false;
        }

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

        var resolved = await TryResolveAsync(collection, registrations, cancellationToken);
        collection.MetadataCheckedAt = timeProvider.GetLocalNow();
        await SaveChangesSafelyAsync(collection, cancellationToken);

        return resolved;
    }

    private async Task<(bool FoundCollections, int ResolvedCount)> ResolveBatchAsync(
        IReadOnlyList<ActiveSeriesDatabaseRegistrationReadModel> registrations,
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

            var resolved = await TryResolveAsync(collection, registrations, cancellationToken);
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
        IReadOnlyList<ActiveSeriesDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        if (collection.Metadata is not null)
        {
            return false;
        }

        var lookup = new MediaMetadataLookup(
            MediaKind: MediaKind.TvSeries,
            ImdbId: ExtractImdbId(collection),
            Title: ExtractSeriesTitle(collection.Name),
            Year: null,
            SeasonNumber: null,
            EpisodeNumber: null,
            LanguageCode: collection.PrimaryLanguageCode
        );

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var metadataDatabase = metadataDatabaseFactory.Get(
                    registration.SeriesDatabaseClassName
                );
                
                var config = metadataDatabase.DeserializeConfig(registration.SerializedConfig);

                var metadata = await ResolveMetadataAsync(
                    metadataDatabase: metadataDatabase,
                    config: config,
                    lookup: lookup,
                    cancellationToken: cancellationToken
                );

                if (metadata is null)
                {
                    continue;
                }

                collection.Metadata = new ReleaseCollectionMetadata
                {
                    SeriesDatabaseClassName = registration.SeriesDatabaseClassName,
                    Title = metadata.Title,
                    Description = metadata.Description,
                    CoverUrl = metadata.CoverUrl,
                    SeriesDatabaseUrl = metadata.DatabaseUrl,
                };

                logger.LogInformation(
                    "Resolved metadata for release collection {CollectionName} using {SeriesDatabase}",
                    collection.Name,
                    registration.SeriesDatabaseClassName
                );

                return true;
            }
            catch (MediaMetadataDatabaseRateLimitExceededException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rate limit reached while resolving metadata for collection {CollectionName} using {SeriesDatabase}",
                    collection.Name,
                    registration.SeriesDatabaseClassName
                );

                return false;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve metadata for collection {CollectionName} using {SeriesDatabase}",
                    collection.Name,
                    registration.SeriesDatabaseClassName
                );
            }
        }

        return false;
    }

    private static async Task<MediaMetadata?> ResolveMetadataAsync(
        IMediaMetadataDatabase metadataDatabase,
        IMediaMetadataDatabaseConfig config,
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(lookup.ImdbId))
        {
            var mediaMetadata = await metadataDatabase.GetByImdbIdAsync(config, lookup, cancellationToken);

            if (mediaMetadata is not null)
            {
                return mediaMetadata;
            }
        }

        if (!string.IsNullOrWhiteSpace(lookup.Title))
        {
            return await metadataDatabase.GetByTitleAsync(config, lookup, cancellationToken);
        }

        return null;
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

        var seasonMatch = SeasonMarkerRegex().Match(normalized);

        if (seasonMatch.Success)
        {
            normalized = normalized[..seasonMatch.Index];
        }

        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<
        IReadOnlyList<ActiveSeriesDatabaseRegistrationReadModel>
    > GetActiveSeriesDatabaseRegistrationsAsync(CancellationToken cancellationToken)
    {
        activeRegistrations ??= (
            await repository.GetActiveSeriesDatabaseRegistrationsAsync(cancellationToken)
        )
            .OrderBy(registration =>
                metadataDatabaseFactory.Get(registration.SeriesDatabaseClassName).ResolutionPriority
            )
            .ThenBy(registration => registration.SeriesDatabaseClassName)
            .ToList();

        return activeRegistrations;
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

    [GeneratedRegex(@"\b(S\d{1,2}(E\d{1,3})?|Season|Staffel)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
