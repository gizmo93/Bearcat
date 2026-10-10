using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveCleanupRepository
{
    Task<IReadOnlyList<Archive>> GetLocalArchivesPastRetentionAsync(
        DateTime cutoff,
        CancellationToken cancellationToken
    );

    Task<bool> HasWaitingPendingOrUploadingUploadAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ArchiveStorageFolder>> GetActiveArchiveStorageFoldersAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<int>> GetArchiveIdsWithUnresolvedNotificationAsync(
        NotificationKind notificationKind,
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
