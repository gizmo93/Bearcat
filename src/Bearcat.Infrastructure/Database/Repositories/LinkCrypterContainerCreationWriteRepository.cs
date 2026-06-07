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

    public async Task<LinkCrypterContainer?> GetCollectionContainerAsync(
        int collectionUploadSlotId,
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .LinkCrypterContainers.Include(container => container.SourceUploads)
            .FirstOrDefaultAsync(
                container =>
                    container.Scope == LinkCrypterContainerScope.ReleaseCollection
                    && container.CollectionUploadSlotId == collectionUploadSlotId
                    && container.LinkCrypterRegistrationId == linkCrypterRegistrationId,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<Upload>> GetCompletedOnlineUploadsByCollectionSlotAsync(
        int collectionUploadSlotId,
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(upload => upload.UploadedFiles)
            .Include(upload => upload.UploadConfig)
                .ThenInclude(config => config.Release)
            .Include(upload => upload.UploadConfig)
                .ThenInclude(config => config.CollectionUploadSlot)
                    .ThenInclude(slot => slot!.ReleaseCollection)
            .Include(upload => upload.UploadConfig)
                .ThenInclude(config => config.LinkCrypters)
                    .ThenInclude(linkCrypter => linkCrypter.LinkCrypterRegistration)
            .Where(upload =>
                upload.UploadState == UploadState.Completed
                && upload.OnlineState == OnlineState.Online
                && upload.UploadedFiles.Any()
                && upload.UploadConfig.CollectionUploadSlotId == collectionUploadSlotId
                && upload.UploadConfig.LinkCrypters.Any(linkCrypter =>
                    linkCrypter.ContainerScope == LinkCrypterContainerScope.ReleaseCollection
                    && linkCrypter.LinkCrypterRegistrationId == linkCrypterRegistrationId
                    && linkCrypter.LinkCrypterRegistration.IsActive
                )
            )
            .ToListAsync(cancellationToken);
    }

    public void Add(LinkCrypterContainer container)
    {
        dbWrite.Add(container);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
