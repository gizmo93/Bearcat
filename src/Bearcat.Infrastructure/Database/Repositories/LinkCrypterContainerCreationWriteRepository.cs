using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class LinkCrypterContainerCreationWriteRepository(IBearcatWriteDbContext dbWrite)
    : ILinkCrypterContainerCreationWriteRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsWithMissingLinkCrypterContainersAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.CollectionUploadSlot)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.LinkCrypters)
                    .ThenInclude(l => l.LinkCrypterRegistration)
            .Include(u => u.LinkCrypterContainers)
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Uploads)
                    .ThenInclude(u => u.LinkCrypterContainers)
            .Where(u =>
                u.UploadState == UploadState.Completed
                && u.OnlineState == OnlineState.Online
                && u.UploadedFiles.Any()
                && u.UploadConfig.LinkCrypters.Any(l =>
                    l.LinkCrypterRegistration.IsActive
                    && (
                        (
                            l.ContainerScope == LinkCrypterContainerScope.Release
                            && !u
                                .LinkCrypterContainers.Select(c => c.UploadConfigLinkCrypterId)
                                .Contains(l.Id)
                        )
                        || (
                            l.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                            && u.UploadConfig.CollectionUploadSlotId != null
                            && !dbWrite.LinkCrypterContainerSourceUploads.Any(source =>
                                source.UploadId == u.Id
                                && source.LinkCrypterContainer.CollectionUploadSlotId
                                    == u.UploadConfig.CollectionUploadSlotId
                                && source.LinkCrypterContainer.LinkCrypterRegistrationId
                                    == l.LinkCrypterRegistrationId
                            )
                        )
                    )
                )
            )
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<
        IReadOnlyList<LinkCrypterContainer>
    > GetReleaseContainersWithUpdateRetryDueAsync(
        DateTime updateFailedBefore,
        CancellationToken cancellationToken
    )
    {
        return await QueryReleaseContainersWithUpload()
            .Where(container =>
                container.Scope == LinkCrypterContainerScope.Release
                && container.State == LinkCrypterContainerState.UpdateFailed
                && (
                    container.UpdateFailedAt == null
                    || container.UpdateFailedAt < updateFailedBefore
                )
                && container.UploadConfigLinkCrypter!.LinkCrypterRegistration.IsActive
                && container.Upload!.UploadState == UploadState.Completed
                && container.Upload.OnlineState == OnlineState.Online
                && container.Upload.UploadedFiles.Any()
            )
            .OrderBy(container => container.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterContainer> GetReleaseContainerWithUploadAsync(
        int containerId,
        CancellationToken cancellationToken
    )
    {
        return await QueryReleaseContainersWithUpload()
            .FirstAsync(
                container =>
                    container.Id == containerId
                    && container.Scope == LinkCrypterContainerScope.Release,
                cancellationToken
            );
    }

    private IQueryable<LinkCrypterContainer> QueryReleaseContainersWithUpload()
    {
        return dbWrite
            .LinkCrypterContainers.AsSplitQuery()
            .Include(container => container.Upload!)
                .ThenInclude(upload => upload.UploadedFiles)
            .Include(container => container.Upload!)
                .ThenInclude(upload => upload.UploadConfig)
                    .ThenInclude(uploadConfig => uploadConfig.Release)
            .Include(container => container.UploadConfigLinkCrypter!)
                .ThenInclude(linkCrypter => linkCrypter.LinkCrypterRegistration);
    }

    public async Task<CollectionUploadSlot> GetCollectionUploadSlotAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .CollectionUploadSlots.AsSplitQuery()
            .Include(slot => slot.ReleaseCollection)
            .Include(slot => slot.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.ArchiveConfig)
            .Include(slot => slot.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.LinkCrypters)
                    .ThenInclude(linkCrypter => linkCrypter.LinkCrypterRegistration)
            .Include(slot => slot.UploadConfigs)
                .ThenInclude(uploadConfig => uploadConfig.Uploads)
                    .ThenInclude(upload => upload.UploadedFiles)
            .FirstAsync(slot => slot.Id == collectionUploadSlotId, cancellationToken);
    }

    public async Task<IReadOnlyList<LinkCrypterContainer>> GetCollectionContainersAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterContainers.Include(container => container.SourceUploads)
            .Where(container =>
                container.Scope == LinkCrypterContainerScope.ReleaseCollection
                && container.CollectionUploadSlotId == collectionUploadSlotId
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<LinkCrypterContainer?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.LinkCrypterContainers.FirstOrDefaultAsync(
            container => container.Id == id,
            cancellationToken
        );
    }

    public void Add(LinkCrypterContainer container)
    {
        dbWrite.Add(container);
    }

    public void Remove(LinkCrypterContainer container)
    {
        dbWrite.Remove(container);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
