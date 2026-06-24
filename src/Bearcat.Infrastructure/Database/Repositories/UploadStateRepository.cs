using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadStateRepository(IBearcatWriteDbContext dbWrite) : IUploadStateRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsToCheckAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    )
    {
        List<UploadState> uploadStatesToExclude =
        [
            UploadState.Canceled,
            UploadState.CancellationRequested,
            UploadState.Failed,
        ];

        var lastCheckThreshold = localNow.AddMinutes(-30);

        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseGroup)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.UploadedFiles)
            .Where(u =>
                (
                    u.OnlineState == OnlineState.Online
                    || u.OnlineState == OnlineState.PartiallyOnline
                )
                && !uploadStatesToExclude.Contains(u.UploadState)
                && u.UploadConfig.HosterRegistration.IsActive
                && u.UploadedFiles.Any(f => f.CheckedAt == null || f.CheckedAt < lastCheckThreshold)
                && !dbWrite.Uploads.Any(newer =>
                    newer.UploadConfigId == u.UploadConfigId
                    && newer.Id > u.Id
                    && newer.UploadState == UploadState.Completed
                )
            )
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetUploadsEligibleForAutomaticReuploadAsync(
        CancellationToken cancellationToken
    )
    {
        List<UploadState> pendingStates =
        [
            UploadState.Pending,
            UploadState.Uploading,
            UploadState.WaitingForArchive,
            UploadState.Failed,
            UploadState.CancellationRequested,
        ];

        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseGroup)
                        .ThenInclude(g => g.QualityProfile!)
                            .ThenInclude(p => p.Rules)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseInfo!)
                        .ThenInclude(i => i.ReleaseNfo)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.MediaFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.QualityIssues)
            .Where(u =>
                (
                    u.OnlineState == OnlineState.Offline
                    || u.OnlineState == OnlineState.PartiallyOnline
                )
                && u.UploadState == UploadState.Completed
                && u.UploadConfig.HosterRegistration.IsActive
                && u.UploadConfig.Release.ReleaseGroup.EnableAutomaticReuploads
                && !u.UploadConfig.Uploads.Any(ru =>
                    ru.OnlineState == OnlineState.Online
                    || pendingStates.Contains(ru.UploadState)
                    || (ru.Id > u.Id && ru.UploadState == UploadState.Canceled)
                )
            )
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<Upload> GetUploadForReuploadAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Uploads)
            .FirstAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<Upload?> GetByIdAsync(int uploadId, CancellationToken cancellationToken)
    {
        return await dbWrite.Uploads.FirstOrDefaultAsync(u => u.Id == uploadId, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        DateTime releaseCreatedBefore,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .UploadConfigs.AsSplitQuery()
            .Include(u => u.Release)
                .ThenInclude(r => r.ReleaseGroup)
                    .ThenInclude(g => g.QualityProfile!)
                        .ThenInclude(p => p.Rules)
            .Include(u => u.Release)
                .ThenInclude(r => r.ReleaseInfo!)
                    .ThenInclude(i => i.ReleaseNfo)
            .Include(u => u.Release)
                .ThenInclude(r => r.MediaFiles)
            .Include(u => u.Release)
                .ThenInclude(r => r.QualityIssues)
            .Where(u =>
                !u.Uploads.Any()
                && u.HosterRegistration.IsActive
                && u.Release.CreatedAt <= releaseCreatedBefore
            )
            .ToListAsync(cancellationToken);
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
