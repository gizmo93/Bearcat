using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseFolderRetirementRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseFolderRetirementRepository
{
    public async Task<IReadOnlyList<Release>> GetConversionCandidatesAsync(
        DateTime cutoff,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.Archives)
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.UploadConfigs)
                    .ThenInclude(uc => uc.HosterRegistration)
            .Include(r => r.ArchiveConfigs)
                .ThenInclude(c => c.UploadConfigs)
                    .ThenInclude(uc => uc.Uploads)
                        .ThenInclude(u => u.UploadedFiles)
            .Where(r =>
                r.ReleaseType == ReleaseType.Managed
                && !r.ExcludeFromAutoCleanup
                && r.ReleaseFolderPath != null
                && (
                    (r.UploadsPostedAt != null && r.UploadsPostedAt <= cutoff)
                    || (
                        r.UploadsPostedAt == null
                        && dbWrite
                            .Uploads.Where(u =>
                                u.UploadConfig.ReleaseId == r.Id && u.UploadedAt != null
                            )
                            .Min(u => u.UploadedAt) <= cutoff
                    )
                )
                && !dbWrite.Uploads.Any(u =>
                    u.UploadConfig.ReleaseId == r.Id
                    && (
                        u.UploadState == UploadState.WaitingForArchive
                        || u.UploadState == UploadState.Pending
                        || u.UploadState == UploadState.Uploading
                    )
                )
            )
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
