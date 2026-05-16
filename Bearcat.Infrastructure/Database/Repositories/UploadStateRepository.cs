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
        List<OnlineState> onlineStatesToCheck = [OnlineState.Online, OnlineState.PartiallyOnline];

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
                onlineStatesToCheck.Contains(u.OnlineState)
                && u.UploadedFiles.Any(f => f.CheckedAt == null || f.CheckedAt < lastCheckThreshold)
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
        ];

        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
                    .ThenInclude(r => r.ReleaseGroup)
            .Where(u =>
                (
                    u.OnlineState == OnlineState.Offline
                    || u.OnlineState == OnlineState.PartiallyOnline
                )
                && u.UploadConfig.Release.ReleaseGroup.EnableAutomaticReuploads
                && !u.UploadConfig.Uploads.Any(ru =>
                    ru.OnlineState == OnlineState.Online || pendingStates.Contains(ru.UploadState)
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

    public async Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .UploadConfigs.Where(u => !u.Uploads.Any())
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
}
