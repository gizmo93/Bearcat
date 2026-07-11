using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseCollectionInfoRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseCollectionInfoRepository
{
    public async Task<IReadOnlyList<ReleaseCollection>> GetCollectionsWithoutMetadataAsync(
        int count,
        DateTime lastCheckedThreshold,
        HashSet<int> excludedCollectionIds,
        CancellationToken cancellationToken = default
    )
    {
        return await IncludeReleaseInfo(dbWrite.ReleaseCollections)
            .Where(collection => collection.Metadata == null)
            .Where(collection =>
                collection.MetadataCheckedAt == null
                || collection.MetadataCheckedAt < lastCheckedThreshold
            )
            .Where(collection => !excludedCollectionIds.Contains(collection.Id))
            .OrderBy(collection => collection.CreatedAt)
            .ThenBy(collection => collection.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseCollection?> GetByIdForResolutionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await IncludeReleaseInfo(dbWrite.ReleaseCollections)
            .Include(collection => collection.Metadata)
            .FirstOrDefaultAsync(
                collection => collection.Id == releaseCollectionId,
                cancellationToken
            );
    }

    public void DetachPendingMetadata(ReleaseCollection collection)
    {
        var pendingMetadata = dbWrite
            .ChangeTracker.Entries<ReleaseCollectionMetadata>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        foreach (var entry in pendingMetadata)
        {
            entry.State = EntityState.Detached;
        }

        if (
            collection.Metadata is not null
            && pendingMetadata.Any(entry => entry.Entity == collection.Metadata)
        )
        {
            collection.Metadata = null;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<ReleaseCollection> IncludeReleaseInfo(
        IQueryable<ReleaseCollection> query
    )
    {
        return query
            .Include(collection => collection.Releases)
                .ThenInclude(release => release.ReleaseNfo)
            .Include(collection => collection.Releases)
                .ThenInclude(release => release.ExternalIdentifiers)
            .Include(collection => collection.Releases)
                .ThenInclude(release => release.ReleaseInfo)
                    .ThenInclude(info => info!.ExternalInfos);
    }
}
