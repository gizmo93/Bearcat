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
            .Where(u => u.ArchiveId == null && u.UploadState == UploadState.WaitingForArchive)
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<int?> GetPossibleAssignableArchiveId(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        var archiveId = await dbWrite
            .Archives.Where(a =>
                a.ArchiveConfigId == archiveConfigId && a.ArchiveState == ArchiveState.Created
            )
            .OrderByDescending(a => a.Id)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        return archiveId > 0 ? archiveId : null;
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

    public async Task DeleteOrphanedArchivesAsync(CancellationToken cancellationToken)
    {
        await dbWrite
            .Archives.Where(a => a.ArchiveState == ArchiveState.Creating)
            .ExecuteDeleteAsync(cancellationToken);
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
