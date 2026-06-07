using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;

public interface IReleaseCollectionWriteRepository
{
    Task<ReleaseCollection?> GetByReleaseGroupAndKeyAsync(
        int releaseGroupId,
        string key,
        CancellationToken cancellationToken = default
    );

    Task<ReleaseCollection> GetByIdAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<CollectionUploadSlot> GetUploadSlotForSharedLinkCrypterUpdateAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    );

    Task<CollectionUploadSlot> GetUploadSlotForDeleteAsync(
        int collectionUploadSlotId,
        CancellationToken cancellationToken = default
    );

    Task<bool> UploadSlotKeyExistsAsync(
        int releaseCollectionId,
        string key,
        CancellationToken cancellationToken = default
    );

    Task<int> GetReleaseCountAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CollectionReleaseArchiveConfigTarget>> GetArchiveConfigTargetsAsync(
        int releaseCollectionId,
        string archiveConfigName,
        CancellationToken cancellationToken = default
    );

    void Add(ReleaseCollection releaseCollection);

    void Add(CollectionUploadSlot uploadSlot);

    void Remove(CollectionUploadSlot uploadSlot);

    void Remove(UploadConfig uploadConfig);

    void Remove(UploadConfigLinkCrypter uploadConfigLinkCrypter);

    void Remove(ReleaseCollection releaseCollection);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
