using Bearcat.Abstractions.MediaMetadataDatabase;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ResolveMediaMetadata;

public class MediaMetadataResolver(
    IMediaMetadataResolverRepository repository,
    IMediaMetadataDatabaseFactory databaseFactory,
    ILogger<MediaMetadataResolver> logger
)
{
    private IReadOnlyList<(
        MediaMetadataDatabaseRegistration Registration,
        IMediaMetadataDatabase Database
    )>? activeDatabases;

    public async Task<ResolvedMediaMetadata?> ResolveAsync(
        MediaMetadataLookup lookup,
        CancellationToken cancellationToken = default
    )
    {
        activeDatabases ??= (await repository.GetActiveRegistrationsAsync(cancellationToken))
            .Select(registration =>
                (Registration: registration, Database: databaseFactory.Get(registration.ClassName))
            )
            .OrderBy(item => item.Database.ResolutionPriority)
            .ThenBy(item => item.Registration.ClassName)
            .ToList();

        foreach (var (registration, database) in activeDatabases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!database.SupportedMediaKinds.Contains(lookup.MediaKind))
                {
                    continue;
                }

                var config = database.DeserializeConfig(registration.SerializedConfig);
                MediaMetadata? metadata = null;

                if (!string.IsNullOrWhiteSpace(lookup.ImdbId))
                {
                    metadata = await database.GetByImdbIdAsync(config, lookup, cancellationToken);
                }

                if (metadata is null && !string.IsNullOrWhiteSpace(lookup.Title))
                {
                    metadata = await database.GetByTitleAsync(config, lookup, cancellationToken);
                }

                if (metadata is not null)
                {
                    return new ResolvedMediaMetadata(registration.ClassName, metadata);
                }
            }
            catch (MediaMetadataDatabaseRateLimitExceededException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rate limit reached while resolving {MediaKind} metadata using {MetadataDatabase}",
                    lookup.MediaKind,
                    registration.ClassName
                );
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve {MediaKind} metadata using {MetadataDatabase}",
                    lookup.MediaKind,
                    registration.ClassName
                );
            }
        }

        return null;
    }
}
