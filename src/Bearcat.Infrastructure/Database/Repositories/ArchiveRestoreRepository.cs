using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveRestoreRepository(IBearcatWriteDbContext dbWrite) : IArchiveRestoreRepository
{
    public async Task<IReadOnlyList<Archive>> GetInterruptedRestoresAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(a => a.ArchiveState == ArchiveState.Restoring)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetUploadsWaitingForRestoreAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Where(u =>
                u.ArchiveId == null
                && u.UploadState == UploadState.WaitingForArchive
                && dbWrite
                    .Archives.Where(a => a.ArchiveConfigId == u.UploadConfig.ArchiveConfigId)
                    .OrderByDescending(a => a.Id)
                    .Take(1)
                    .Any(a =>
                        a.ArchiveState == ArchiveState.Deleted
                        || a.ArchiveState == ArchiveState.MissingFiles
                    )
            )
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Archive?> GetLatestArchiveAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.AsSplitQuery()
            .Include(a => a.ArchiveConfig)
            .Include(a => a.ArchiveFiles)
            .Where(a => a.ArchiveConfigId == archiveConfigId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Upload>> GetUploadsOfArchiveAsync(
        int archiveId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.AsSplitQuery()
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterRegistration)
            .Include(u => u.UploadedFiles)
            .Where(u => u.ArchiveId == archiveId)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GetSerializedConfigAsync(
        int hosterRegistrationId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .HosterRegistrations.Where(h => h.Id == hosterRegistrationId)
            .Select(h => h.SerializedConfig)
            .FirstAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
