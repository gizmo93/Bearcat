using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageLinkCrypterContainers.Repositories;

public interface ILinkCrypterContainerCreationWriteRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsWithMissingLinkCrypterContainersAsync(
        CancellationToken cancellationToken
    );

    Task<CollectionUploadSlot> GetCollectionUploadSlotAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<LinkCrypterContainer>> GetCollectionContainersAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken
    );

    void Add(LinkCrypterContainer container);
    void Remove(LinkCrypterContainer container);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
