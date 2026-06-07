using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseCollectionRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IReleaseCollectionReadRepository, IReleaseCollectionWriteRepository
{
    public async Task<ReleaseCollection?> GetByReleaseGroupAndKeyAsync(
        int releaseGroupId,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseCollections.Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
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
        var collection = await dbRead
            .ReleaseCollections.AsNoTracking()
            .AsSplitQuery()
            .Include(collection => collection.ReleaseGroup)
            .Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
                        .ThenInclude(linkCrypter => linkCrypter.LinkCrypterRegistration)
            .Include(collection => collection.Releases)
            .FirstOrDefaultAsync(
                collection => collection.Id == releaseCollectionId,
                cancellationToken
            );

        if (collection is null)
        {
            return null;
        }

        var containersByUploadSlotId = await dbRead
            .LinkCrypterContainers.Where(container =>
                container.Scope == LinkCrypterContainerScope.ReleaseCollection
                && container.CollectionUploadSlotId != null
                && container.CollectionUploadSlot!.ReleaseCollectionId == releaseCollectionId
            )
            .OrderBy(container => container.LinkCrypterRegistration.Name)
            .ThenBy(container => container.Id)
            .Select(container => new
            {
                CollectionUploadSlotId = container.CollectionUploadSlotId!.Value,
                Container = new CollectionUploadSlotContainerReadModel(
                    container.Id,
                    container.LinkCrypterRegistration.Name,
                    container.ContainerUrl,
                    container.State,
                    container.CreatedAt,
                    container.SourceUploads.Count,
                    container.Errors.ToList()
                ),
            })
            .ToListAsync(cancellationToken);

        var containersBySlotId = containersByUploadSlotId
            .GroupBy(container => container.CollectionUploadSlotId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<CollectionUploadSlotContainerReadModel>)
                        group.Select(container => container.Container).ToList()
            );

        return new ReleaseCollectionDetailReadModel(
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
                    slot.UploadConfigs.Count,
                    GetSharedLinkCrypters(slot),
                    containersBySlotId.TryGetValue(slot.Id, out var containers)
                        ? containers
                        : []
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
        );
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

    public async Task<CollectionUploadSlot> GetUploadSlotForSharedLinkCrypterUpdateAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .CollectionUploadSlots.Include(slot => slot.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .FirstAsync(slot => slot.Id == collectionUploadSlotId, cancellationToken);
    }

    public void Add(ReleaseCollection releaseCollection)
    {
        dbWrite.Add(releaseCollection);
    }

    public void Remove(UploadConfigLinkCrypter uploadConfigLinkCrypter)
    {
        dbWrite.Remove(uploadConfigLinkCrypter);
    }

    public void Remove(ReleaseCollection releaseCollection)
    {
        dbWrite.Remove(releaseCollection);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<CollectionUploadSlotLinkCrypterReadModel> GetSharedLinkCrypters(
        CollectionUploadSlot slot
    )
    {
        return slot
            .UploadConfigs.SelectMany(uploadConfig => uploadConfig.LinkCrypters)
            .Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
            )
            .GroupBy(linkCrypter => new
            {
                linkCrypter.LinkCrypterRegistrationId,
                linkCrypter.LinkCrypterRegistration.Name,
                linkCrypter.LinkCrypterRegistration.IsActive,
            })
            .OrderBy(group => group.Key.Name)
            .Select(group =>
            {
                var settings = group.First();

                return new CollectionUploadSlotLinkCrypterReadModel(
                    group.Key.LinkCrypterRegistrationId,
                    group.Key.Name,
                    group.Key.IsActive,
                    settings.Password,
                    settings.EnableCaptcha,
                    settings.EnableContainerDownload,
                    settings.EnableClickAndLoad,
                    group.Count()
                );
            })
            .ToList();
    }
}
