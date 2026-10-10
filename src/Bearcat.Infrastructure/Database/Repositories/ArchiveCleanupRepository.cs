using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveCleanupRepository(IBearcatWriteDbContext dbWrite) : IArchiveCleanupRepository
{
    private static readonly UploadState[] WaitingPendingOrUploadingStates =
    [
        UploadState.WaitingForArchive,
        UploadState.Pending,
        UploadState.Uploading,
    ];

    public async Task<IReadOnlyList<Archive>> GetLocalArchivesPastRetentionAsync(
        DateTime cutoff,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.AsSplitQuery()
            .Include(a => a.Uploads)
            .Include(a => a.ArchiveFiles)
            .Include(a => a.ArchiveConfig)
                .ThenInclude(c => c.Release)
            .Include(a => a.ArchiveConfig)
                .ThenInclude(c => c.UploadConfigs)
                    .ThenInclude(uc => uc.HosterRegistration)
            .Include(a => a.ArchiveConfig)
                .ThenInclude(c => c.UploadConfigs)
                    .ThenInclude(uc => uc.Uploads)
                        .ThenInclude(u => u.UploadedFiles)
            .Where(a =>
                a.ArchiveState == ArchiveState.Created
                && a.ArchiveStorageFolderId == null
                && a.Uploads.Any()
                && a.Uploads.All(u => u.UploadedAt != null)
                && !a.ArchiveConfig.Release.ExcludeFromAutoCleanup
                && dbWrite
                    .Uploads.Where(u =>
                        u.UploadConfig.ArchiveConfigId == a.ArchiveConfigId && u.UploadedAt != null
                    )
                    .Max(u => u.UploadedAt) <= cutoff
                && !dbWrite.Uploads.Any(u =>
                    u.UploadConfig.ArchiveConfigId == a.ArchiveConfigId
                    && WaitingPendingOrUploadingStates.Contains(u.UploadState)
                )
            )
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasWaitingPendingOrUploadingUploadAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Uploads.AnyAsync(
            u =>
                u.UploadConfig.ArchiveConfigId == archiveConfigId
                && WaitingPendingOrUploadingStates.Contains(u.UploadState),
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ArchiveStorageFolder>> GetActiveArchiveStorageFoldersAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ArchiveStorageFolders.Where(f => f.IsActive)
            .OrderBy(f => f.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetArchiveIdsWithUnresolvedNotificationAsync(
        NotificationKind notificationKind,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Notifications.Where(n =>
                n.NotificationKind == notificationKind
                && n.ResolvedAt == null
                && n.ArchiveId != null
            )
            .Select(n => n.ArchiveId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
