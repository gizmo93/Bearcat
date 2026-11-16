using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveFolderRelocationRepository(IBearcatWriteDbContext dbWrite)
    : IArchiveFolderRelocationRepository
{
    public async Task<IReadOnlyList<Archive>> GetArchivesWithFilesUploadsAndReleaseAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.AsSplitQuery()
            .Include(archive => archive.ArchiveFiles)
            .Include(archive => archive.Uploads)
            .Include(archive => archive.ArchiveConfig)
                .ThenInclude(archiveConfig => archiveConfig.Release)
            .Where(archive => EF.Parameter(archiveIds).Contains(archive.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<int>> GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(archive =>
                EF.Parameter(archiveIds).Contains(archive.Id)
                && !dbWrite.Archives.Any(newerArchive =>
                    newerArchive.ArchiveConfigId == archive.ArchiveConfigId
                    && newerArchive.Id > archive.Id
                )
            )
            .Select(archive => archive.Id)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetFolderPathsUsedByCreatedArchivesAsync(
        IReadOnlyList<string> folderPaths,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Archives.Where(archive =>
                archive.ArchiveState == ArchiveState.Created
                && EF.Parameter(folderPaths).Contains(archive.ArchiveFolderPath)
            )
            .Select(archive => archive.ArchiveFolderPath)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArchiveStorageFolder>> GetArchiveStorageFoldersAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .ArchiveStorageFolders.OrderBy(storageFolder => storageFolder.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
