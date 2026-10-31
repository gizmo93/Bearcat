using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadStateRepository(IBearcatWriteDbContext dbWrite) : IUploadStateRepository
{
    public async Task<IReadOnlyList<HosterRegistration>> GetActiveHosterRegistrationsAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .HosterRegistrations.Where(r => r.IsActive)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetLeastRecentlyCheckedUploadsToCheckAsync(
        int hosterRegistrationId,
        DateTime localNow,
        int maximumUploadCount,
        CancellationToken cancellationToken
    )
    {
        var lastCheckThreshold = localNow.AddMinutes(-30);

        var uploadIds = await BuildUploadsToCheckOnlineStateQuery()
            .Where(u =>
                u.HosterRegistrationId == hosterRegistrationId
                && u.OldestUploadedFileCheckedAt < lastCheckThreshold
            )
            .OrderBy(u => u.OldestUploadedFileCheckedAt)
            .ThenBy(u => u.Id)
            .Select(u => u.Id)
            .Take(maximumUploadCount)
            .ToListAsync(cancellationToken);

        if (uploadIds.Count == 0)
        {
            return [];
        }

        return await dbWrite
            .Uploads.Include(u => u.UploadedFiles)
            .Where(u => uploadIds.Contains(u.Id))
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetUploadsOfReleasesToCheckOnlineStateAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken
    )
    {
        return await BuildUploadsToCheckOnlineStateQuery()
            .AsSplitQuery()
            .Include(u => u.HosterRegistration)
            .Include(u => u.UploadedFiles)
            .Where(u =>
                releaseIds.Contains(u.UploadConfig.ReleaseId)
                && u.HosterRegistration!.IsActive
                && u.UploadedFiles.Any()
            )
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Upload> BuildUploadsToCheckOnlineStateQuery()
    {
        List<UploadState> uploadStatesToExclude =
        [
            UploadState.Canceled,
            UploadState.CancellationRequested,
            UploadState.Failed,
        ];

        return dbWrite.Uploads.Where(u =>
            (
                u.OnlineState == OnlineState.Online
                || u.OnlineState == OnlineState.PartiallyOnline
                || u.OnlineState == OnlineState.Offline
            )
            && !uploadStatesToExclude.Contains(u.UploadState)
            && !dbWrite.Uploads.Any(newer =>
                newer.UploadConfigId == u.UploadConfigId
                && newer.Id > u.Id
                && newer.UploadState == UploadState.Completed
            )
        );
    }

    public async Task<IReadOnlyList<Upload>> GetUploadsDueForAutomaticReuploadAsync(
        DateTime localNow,
        int maximumUploadCount,
        CancellationToken cancellationToken
    )
    {
        List<UploadState> pendingStates =
        [
            UploadState.Pending,
            UploadState.Uploading,
            UploadState.WaitingForArchive,
            UploadState.CancellationRequested,
        ];

        var uploadIds = await dbWrite
            .Uploads.Where(u =>
                (
                    u.OnlineState == OnlineState.Offline
                    || u.OnlineState == OnlineState.PartiallyOnline
                )
                && u.UploadState == UploadState.Completed
                && (u.UploadConfig.HosterAccountPoolId != null || u.HosterRegistration!.IsActive)
                && u.UploadConfig.Release.ReleaseGroup.EnableAutomaticReuploads
                && u.UploadConfig.Release.QualityGateState != QualityGateState.Failed
                && u.UploadedFiles.Any()
                && !u.UploadConfig.Uploads.Any(ru =>
                    ru.OnlineState == OnlineState.Online
                    || pendingStates.Contains(ru.UploadState)
                    || (
                        ru.Id > u.Id
                        && (
                            ru.UploadState == UploadState.Canceled
                            || ru.UploadState == UploadState.Failed
                            || (
                                ru.UploadState == UploadState.Completed
                                && (
                                    ru.OnlineState == OnlineState.Offline
                                    || ru.OnlineState == OnlineState.PartiallyOnline
                                )
                            )
                        )
                    )
                )
                && (
                    u.HosterRegistration!.ReuploadTriggerOverride
                    == ReuploadTrigger.OnlyWhenFullyOffline
                        ? u.FullyOfflineSince
                        : u.NotFullyOnlineSince
                )
                    <= localNow.AddHours(
                        -(
                            u.HosterRegistration!.NumberOfHoursUntilReuploadOverride
                            ?? u.UploadConfig.Release.ReleaseGroup.NumberOfHoursUntilReupload
                        )
                    )
            )
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .Take(maximumUploadCount)
            .ToListAsync(cancellationToken);

        if (uploadIds.Count == 0)
        {
            return [];
        }

        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterAccountPool!)
                    .ThenInclude(pool => pool.Members)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseGroup)
                        .ThenInclude(g => g.QualityProfile!)
                            .ThenInclude(p => p.Rules)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseInfo)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.Metadata)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseNfo)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.MediaFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.QualityIssues)
            .Where(u => uploadIds.Contains(u.Id))
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Upload> GetUploadForReuploadAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Uploads)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterAccountPool!)
                    .ThenInclude(pool => pool.Members)
            .FirstAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<
        IReadOnlyList<Upload>
    > GetLatestUploadOfEachUploadConfigOfReleasesForReuploadAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Uploads)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterAccountPool!)
                    .ThenInclude(pool => pool.Members)
            .Where(u =>
                releaseIds.Contains(u.UploadConfig.ReleaseId)
                && !dbWrite.Uploads.Any(newer =>
                    newer.UploadConfigId == u.UploadConfigId && newer.Id > u.Id
                )
            )
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Upload?> GetUploadForOnlineCheckAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.HosterRegistration)
            .Include(u => u.UploadedFiles)
            .FirstOrDefaultAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<Upload?> GetByIdAsync(int uploadId, CancellationToken cancellationToken)
    {
        return await dbWrite
            .Uploads.Include(u => u.UploadConfig)
            .FirstOrDefaultAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<bool> HasUnresolvedNotificationForHosterRegistrationAsync(
        int hosterRegistrationId,
        NotificationKind kind,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Notifications.AnyAsync(
            n =>
                n.HosterRegistrationId == hosterRegistrationId
                && n.NotificationKind == kind
                && n.ResolvedAt == null,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        DateTime releaseCreatedBefore,
        CancellationToken cancellationToken
    )
    {
        return await BuildUploadConfigsWithoutUploadsQuery()
            .Where(u => u.Release.CreatedAt <= releaseCreatedBefore)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsOfReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        return await BuildUploadConfigsWithoutUploadsQuery()
            .Where(u => u.ReleaseId == releaseId)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<UploadConfig> BuildUploadConfigsWithoutUploadsQuery()
    {
        return dbWrite
            .UploadConfigs.AsSplitQuery()
            .Include(u => u.Release)
                .ThenInclude(r => r.ReleaseGroup)
                    .ThenInclude(g => g.QualityProfile!)
                        .ThenInclude(p => p.Rules)
            .Include(u => u.Release)
                .ThenInclude(r => r.ReleaseInfo)
            .Include(u => u.Release)
                .ThenInclude(r => r.Metadata)
            .Include(u => u.Release)
                .ThenInclude(r => r.ReleaseNfo)
            .Include(u => u.Release)
                .ThenInclude(r => r.MediaFiles)
            .Include(u => u.Release)
                .ThenInclude(r => r.QualityIssues)
            .Where(UploadTargetExpressions.UploadConfigHasActiveHosterRegistration)
            .Where(u => !u.Uploads.Any());
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void Add(Upload upload)
    {
        dbWrite.Add(upload);
    }

    public void Remove(Upload upload)
    {
        dbWrite.Remove(upload);
    }
}
