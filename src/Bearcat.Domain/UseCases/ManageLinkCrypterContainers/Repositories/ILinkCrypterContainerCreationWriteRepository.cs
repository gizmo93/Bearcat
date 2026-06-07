using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;

public interface ILinkCrypterContainerCreationWriteRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsWithMissingLinkCrypterContainersAsync(
        CancellationToken cancellationToken
    );

    Task<LinkCrypterContainer?> GetCollectionContainerAsync(
        int collectionUploadSlotId,
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetCompletedOnlineUploadsByCollectionSlotAsync(
        int collectionUploadSlotId,
        int linkCrypterRegistrationId,
        CancellationToken cancellationToken
    );

    void Add(LinkCrypterContainer container);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
