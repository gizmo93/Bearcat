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
            var searchTerm = $"%{query.SearchTerm.Trim()}%";
            collections = collections.Where(collection =>
                EF.Functions.ILike(collection.Name, searchTerm)
                || EF.Functions.ILike(collection.Key, searchTerm)
            );
        }

        if (query.ReleaseGroupId is not null)
        {
            collections = collections.Where(collection =>
                collection.ReleaseGroupId == query.ReleaseGroupId.Value
            );
        }

        var totalCount = await collections.CountAsync(cancellationToken);

        var pageIndex = Math.Max(0, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);

        var items = await collections
            .OrderBy(collection => collection.Name)
            .ThenBy(collection => collection.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
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
            Items: items,
            TotalCount: totalCount,
            PageIndex: pageIndex,
            PageSize: pageSize
        );
    }

    public async Task<ReleaseCollectionDetailReadModel?> GetDetailAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Select(collection => new
            {
                collection.Id,
                collection.Name,
                collection.Key,
                collection.ReleaseGroupId,
                ReleaseGroupName = collection.ReleaseGroup.Name,
                collection.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null)
        {
            return null;
        }

        var uploadSlots = await dbRead
            .CollectionUploadSlots.Where(slot => slot.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(slot => slot.Name)
            .ThenBy(slot => slot.Id)
            .Select(slot => new
            {
                slot.Id,
                slot.Key,
                slot.Name,
                slot.IsRequired,
                slot.PasswordPolicy,
                slot.ExpectedArchivePassword,
                UploadConfigCount = slot.UploadConfigs.Count,
                UploadCount = slot
                    .UploadConfigs.SelectMany(uploadConfig => uploadConfig.Uploads)
                    .Count(),
            })
            .ToListAsync(cancellationToken);

        var sharedLinkCrypterRows = await dbRead
            .UploadConfigLinkCrypters.Where(linkCrypter =>
                linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                && linkCrypter.UploadConfig.CollectionUploadSlotId != null
                && linkCrypter.UploadConfig.CollectionUploadSlot!.ReleaseCollectionId
                    == releaseCollectionId
            )
            .Select(linkCrypter => new
            {
                CollectionUploadSlotId = linkCrypter.UploadConfig.CollectionUploadSlotId!.Value,
                linkCrypter.UploadConfigId,
                linkCrypter.LinkCrypterRegistrationId,
                LinkCrypterRegistrationName = linkCrypter.LinkCrypterRegistration.Name,
                linkCrypter.LinkCrypterRegistration.IsActive,
                linkCrypter.Password,
                linkCrypter.EnableCaptcha,
                linkCrypter.EnableContainerDownload,
                linkCrypter.EnableClickAndLoad,
            })
            .ToListAsync(cancellationToken);

        var sharedLinkCryptersBySlotId = sharedLinkCrypterRows
            .GroupBy(linkCrypter => linkCrypter.CollectionUploadSlotId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .GroupBy(linkCrypter => new
                        {
                            linkCrypter.LinkCrypterRegistrationId,
                            linkCrypter.LinkCrypterRegistrationName,
                            linkCrypter.IsActive,
                        })
                        .OrderBy(linkCrypterGroup =>
                            linkCrypterGroup.Key.LinkCrypterRegistrationName
                        )
                        .Select(linkCrypterGroup =>
                        {
                            var settings = linkCrypterGroup
                                .OrderBy(linkCrypter => linkCrypter.UploadConfigId)
                                .First();

                            return new CollectionUploadSlotLinkCrypterReadModel(
                                LinkCrypterRegistrationId: linkCrypterGroup
                                    .Key
                                    .LinkCrypterRegistrationId,
                                LinkCrypterRegistrationName: linkCrypterGroup
                                    .Key
                                    .LinkCrypterRegistrationName,
                                IsActive: linkCrypterGroup.Key.IsActive,
                                Password: settings.Password,
                                EnableCaptcha: settings.EnableCaptcha,
                                EnableContainerDownload: settings.EnableContainerDownload,
                                EnableClickAndLoad: settings.EnableClickAndLoad,
                                UploadConfigCount: linkCrypterGroup.Count()
                            );
                        })
                        .ToList()
            );

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
                group => group.Select(container => container.Container).ToList()
            );

        var latestUploads = await dbRead
            .Uploads.Where(upload =>
                upload.UploadConfig.Release.ReleaseCollectionId == releaseCollectionId
            )
            .GroupBy(upload => upload.UploadConfig.ReleaseId)
            .Select(group =>
                group
                    .OrderByDescending(upload => upload.UploadedAt ?? upload.CreatedAt)
                    .ThenByDescending(upload => upload.Id)
                    .Select(upload => new
                    {
                        ReleaseId = upload.UploadConfig.ReleaseId,
                        UploadId = upload.Id,
                        UploadConfigName = upload.UploadConfig.Name,
                    })
                    .First()
            )
            .ToListAsync(cancellationToken);

        var latestUploadsByReleaseId = latestUploads.ToDictionary(upload => upload.ReleaseId);

        var releases = await dbRead
            .Releases.Where(release => release.ReleaseCollectionId == releaseCollectionId)
            .OrderBy(release => release.Name)
            .ThenBy(release => release.Id)
            .Select(release => new
            {
                release.Id,
                release.Name,
                release.ReleaseType,
                release.CreatedAt,
                ActiveUploadConfigsCount = release.UploadConfigs.Count,
                OnlineUploadConfigsCount = release
                    .UploadConfigs.Where(uploadConfig =>
                        uploadConfig.Uploads.Any(upload => upload.OnlineState == OnlineState.Online)
                    )
                    .Distinct()
                    .Count(),
            })
            .ToListAsync(cancellationToken);

        return new ReleaseCollectionDetailReadModel(
            ReleaseCollectionId: collection.Id,
            Name: collection.Name,
            Key: collection.Key,
            ReleaseGroupId: collection.ReleaseGroupId,
            ReleaseGroupName: collection.ReleaseGroupName,
            CreatedAt: collection.CreatedAt,
            UploadSlots: uploadSlots
                .Select(slot =>
                {
                    var sharedLinkCrypters = sharedLinkCryptersBySlotId.TryGetValue(
                        slot.Id,
                        out var linkCrypters
                    )
                        ? linkCrypters
                        : [];

                    var containers = containersBySlotId.TryGetValue(slot.Id, out var slotContainers)
                        ? slotContainers
                        : [];

                    return new CollectionUploadSlotReadModel(
                        CollectionUploadSlotId: slot.Id,
                        Key: slot.Key,
                        Name: slot.Name,
                        IsRequired: slot.IsRequired,
                        PasswordPolicy: slot.PasswordPolicy,
                        ExpectedArchivePassword: slot.ExpectedArchivePassword,
                        UploadConfigCount: slot.UploadConfigCount,
                        UploadCount: slot.UploadCount,
                        SharedLinkCrypters: sharedLinkCrypters,
                        Containers: containers
                    );
                })
                .ToList(),
            Releases: releases
                .Select(release =>
                {
                    latestUploadsByReleaseId.TryGetValue(release.Id, out var latestUpload);

                    return new ReleaseCollectionReleaseReadModel(
                        ReleaseId: release.Id,
                        Name: release.Name,
                        ReleaseType: release.ReleaseType,
                        CreatedAt: release.CreatedAt,
                        ActiveUploadConfigsCount: release.ActiveUploadConfigsCount,
                        OnlineUploadConfigsCount: release.OnlineUploadConfigsCount,
                        LatestUploadId: latestUpload?.UploadId,
                        LatestUploadConfigName: latestUpload?.UploadConfigName
                    );
                })
                .ToList()
        );
    }

    public async Task<
        IReadOnlyList<CollectionArchiveConfigOptionReadModel>
    > GetArchiveConfigOptionsAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseCount = await dbRead.Releases.CountAsync(
            release => release.ReleaseCollectionId == releaseCollectionId,
            cancellationToken
        );

        if (releaseCount == 0)
        {
            return [];
        }

        return await dbRead
            .ArchiveConfigs.Where(config =>
                config.Release.ReleaseCollectionId == releaseCollectionId
            )
            .GroupBy(config => config.Name)
            .Where(group =>
                group.Select(config => config.ReleaseId).Distinct().Count() == releaseCount
            )
            .OrderBy(group => group.Key)
            .Select(group => new CollectionArchiveConfigOptionReadModel(group.Key, releaseCount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableReleaseReadModel>> SearchAvailableReleasesAsync(
        int releaseCollectionId,
        string? searchTerm,
        CancellationToken cancellationToken = default
    )
    {
        var releaseGroupId = await dbRead
            .ReleaseCollections.Where(collection => collection.Id == releaseCollectionId)
            .Select(collection => collection.ReleaseGroupId)
            .FirstAsync(cancellationToken);

        var query = dbRead.Releases.Where(release =>
            release.ReleaseGroupId == releaseGroupId
            && release.ReleaseCollectionId != releaseCollectionId
        );

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(release => EF.Functions.ILike(release.Name, term));
        }

        return await query
            .OrderBy(release => release.Name)
            .Take(50)
            .Select(release => new AvailableReleaseReadModel(release.Id, release.Name))
            .ToListAsync(cancellationToken);
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

    public async Task<ReleaseCollection> GetByIdWithSlotsAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .ReleaseCollections.Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.ArchiveConfig)
            .Include(collection => collection.UploadSlots)
                .ThenInclude(slot => slot.UploadConfigs)
                    .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
            .FirstAsync(collection => collection.Id == releaseCollectionId, cancellationToken);
    }

    public async Task<Release> GetReleaseByIdAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite.Releases.FirstAsync(
            release => release.Id == releaseId,
            cancellationToken
        );
    }

    public async Task<Release> GetReleaseWithSlotUploadConfigsAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release =>
                release.UploadConfigs.Where(uc => uc.CollectionUploadSlotId != null)
            )
            .FirstAsync(release => release.Id == releaseId, cancellationToken);
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

    public async Task<CollectionUploadSlot> GetUploadSlotForDeleteAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .CollectionUploadSlots.Include(slot => slot.UploadConfigs)
            .FirstAsync(slot => slot.Id == collectionUploadSlotId, cancellationToken);
    }

    public async Task<bool> UploadSlotKeyExistsAsync(
        int releaseCollectionId,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.CollectionUploadSlots.AnyAsync(
            slot => slot.ReleaseCollectionId == releaseCollectionId && slot.Key == key,
            cancellationToken
        );
    }

    public async Task<int> GetReleaseCountAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead.Releases.CountAsync(
            release => release.ReleaseCollectionId == releaseCollectionId,
            cancellationToken
        );
    }

    public async Task<
        IReadOnlyList<CollectionReleaseArchiveConfigTarget>
    > GetArchiveConfigTargetsAsync(
        int releaseCollectionId,
        string archiveConfigName,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ArchiveConfigs.Where(config =>
                config.Release.ReleaseCollectionId == releaseCollectionId
                && config.Name == archiveConfigName
            )
            .Select(config => new CollectionReleaseArchiveConfigTarget(config.ReleaseId, config.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<
        IReadOnlyList<CollectionReleaseArchiveConfigTarget>
    > GetArchiveConfigTargetsForReleaseAsync(
        int releaseId,
        IReadOnlyCollection<string> archiveConfigNames,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ArchiveConfigs.Where(config =>
                config.ReleaseId == releaseId && archiveConfigNames.Contains(config.Name)
            )
            .Select(config => new CollectionReleaseArchiveConfigTarget(
                config.ReleaseId,
                config.Id,
                config.Name
            ))
            .ToListAsync(cancellationToken);
    }

    public void Add(ReleaseCollection releaseCollection)
    {
        dbWrite.Add(releaseCollection);
    }

    public void Add(CollectionUploadSlot uploadSlot)
    {
        dbWrite.Add(uploadSlot);
    }

    public void Remove(CollectionUploadSlot uploadSlot)
    {
        dbWrite.Remove(uploadSlot);
    }

    public void Remove(UploadConfig uploadConfig)
    {
        dbWrite.Remove(uploadConfig);
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
}
