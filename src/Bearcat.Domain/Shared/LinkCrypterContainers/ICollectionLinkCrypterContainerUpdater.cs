namespace Bearcat.Domain.Shared.LinkCrypterContainers;

public interface ICollectionLinkCrypterContainerUpdater
{
    Task UpdateContainersAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    );
}
