using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseCollectionRepository(IBearcatReadDbContext dbRead, IBearcatWriteDbContext dbWrite)
    : IReleaseCollectionReadRepository,
        IReleaseCollectionWriteRepository
{
    public async Task<ReleaseCollection?> GetByReleaseGroupAndKeyAsync(
        int releaseGroupId,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseCollections.Include(collection => collection.UploadSlots)
            .FirstOrDefaultAsync(
                collection => collection.ReleaseGroupId == releaseGroupId && collection.Key == key,
                cancellationToken
            );
    }

    public async Task<PagedResult<ReleaseCollectionReadModel>> SearchAsync(
        ReleaseCollectionSearchQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var collections = dbRead.ReleaseCollections.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim().ToLowerInvariant();
            collections = collections.Where(collection =>
                collection.Name.ToLower().Contains(searchTerm)
                || collection.Key.ToLower().Contains(searchTerm)
            );
        }

        if (query.ReleaseGroupId is not null)
        {
            collections = collections.Where(collection =>
                collection.ReleaseGroupId == query.ReleaseGroupId.Value
            );
        }

        var totalCount = await collections.CountAsync(cancellationToken);

        var items = await collections
            .OrderBy(collection => collection.Name)
            .ThenBy(collection => collection.Id)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .Select(collection => new ReleaseCollectionReadModel(
                collection.Id,
                collection.Name,
                collection.Key,
                collection.ReleaseGroupId,
                collection.ReleaseGroup.Name,
                collection.Releases.Count,
                collection.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ReleaseCollectionReadModel>(
            items,
            totalCount,
            query.PageIndex,
            query.PageSize
        );
    }

    public async Task<ReleaseCollectionDetailReadModel?> GetDetailAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Select(collection => new ReleaseCollectionDetailReadModel(
                collection.Id,
                collection.Name,
                collection.Key,
                collection.ReleaseGroupId,
                collection.ReleaseGroup.Name,
                collection.CreatedAt,
                collection
                    .UploadSlots.OrderBy(slot => slot.Name)
                    .ThenBy(slot => slot.Id)
                    .Select(slot => new CollectionUploadSlotReadModel(
                        slot.Id,
                        slot.Key,
                        slot.Name,
                        slot.IsRequired,
                        slot.PasswordPolicy,
                        slot.ExpectedArchivePassword,
                        slot.UploadConfigs.Count
                    ))
                    .ToList(),
                collection
                    .Releases.OrderBy(release => release.Name)
                    .ThenBy(release => release.Id)
                    .Select(release => new ReleaseCollectionReleaseReadModel(
                        release.Id,
                        release.Name,
                        release.ReleaseType,
                        release.CreatedAt
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ReleaseCollection> GetByIdAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.ReleaseCollections.FirstAsync(
            collection => collection.Id == releaseCollectionId,
            cancellationToken
        );
    }

    public void Add(ReleaseCollection releaseCollection)
    {
        dbWrite.Add(releaseCollection);
    }

    public void Remove(ReleaseCollection releaseCollection)
    {
        dbWrite.Remove(releaseCollection);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
