using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveCreationRepository(IBearcatWriteDbContext dbWrite) : IArchiveCreationRepository
{
    public async Task<IReadOnlyList<Upload>> GetUploadsWithoutArchiveAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Uploads.Include(u => u.UploadConfig)
                .ThenInclude(u => u.ArchiveConfig)
                    .ThenInclude(a => a.Release)
            .Include(u => u.UploadConfig)
                .ThenInclude(u => u.HosterRegistration)
            .Where(u => u.ArchiveId == null && u.UploadState == UploadState.WaitingForArchive)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<Archive?> GetPossibleAssignableArchiveAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Include(a => a.ArchiveFiles)
            .Include(a => a.Uploads)
                .ThenInclude(u => u.UploadedFiles)
            .Where(a =>
                a.ArchiveConfigId == archiveConfigId && a.ArchiveState == ArchiveState.Created
            )
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> HasCompletedUploadForHosterAsync(
        int archiveConfigId,
        string hosterClassName,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.Uploads.AnyAsync(
            u =>
                u.UploadConfig.ArchiveConfigId == archiveConfigId
                && u.UploadConfig.HosterRegistration.HosterClassName == hosterClassName
                && u.UploadState == UploadState.Completed,
            cancellationToken
        );
    }

    public async Task<bool> HasActiveUploadAsync(int archiveId, CancellationToken cancellationToken)
    {
        return await dbWrite.Uploads.AnyAsync(
            u =>
                u.ArchiveId == archiveId
                && (
                    u.UploadState == UploadState.Pending
                    || u.UploadState == UploadState.Uploading
                    || u.UploadState == UploadState.CancellationRequested
                ),
            cancellationToken
        );
    }

    public async Task<int?> GetLastArchiveFileSizeMbAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(a => a.ArchiveConfigId == archiveConfigId)
            .OrderByDescending(a => a.Id)
            .Select(a => (int?)a.ArchiveFileSizeMb)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetKnownArchiveFileHashesAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(a => a.ArchiveConfigId == archiveConfigId)
            .SelectMany(a => a.ArchiveFiles)
            .Where(f => f.Md5Hash != null)
            .Select(f => f.Md5Hash!)
            .Distinct()
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> LastArchiveHasFilesWithoutHashAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(a => a.ArchiveConfigId == archiveConfigId && a.ArchiveFiles.Any())
            .OrderByDescending(a => a.Id)
            .Select(a => a.ArchiveFiles.Any(f => f.Md5Hash == null))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Archive>> GetInterruptedArchivesAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Include(a => a.ArchiveFiles)
            .Include(a => a.Uploads)
            .Include(a => a.ArchiveConfig)
            .Where(a => a.ArchiveState == ArchiveState.Creating)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public void Remove(Archive archive)
    {
        dbWrite.Remove(archive);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }

    public void Add(Archive archive)
    {
        dbWrite.Add(archive);
    }
}
