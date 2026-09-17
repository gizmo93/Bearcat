using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveCleanupRepository(IBearcatWriteDbContext dbWrite) : IArchiveCleanupRepository
{
    public async Task<IReadOnlyList<Archive>> GetDeletableArchivesAsync(
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
                    && (
                        u.UploadState == UploadState.WaitingForArchive
                        || u.UploadState == UploadState.Pending
                        || u.UploadState == UploadState.Uploading
                    )
                )
            )
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
