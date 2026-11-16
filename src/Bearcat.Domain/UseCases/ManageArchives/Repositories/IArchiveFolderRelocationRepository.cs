using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveFolderRelocationRepository
{
    Task<IReadOnlyList<Archive>> GetArchivesWithFilesUploadsAndReleaseAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken
    );

    Task<HashSet<int>> GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken
    );

    Task<HashSet<string>> GetFolderPathsUsedByCreatedArchivesAsync(
        IReadOnlyList<string> folderPaths,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<ArchiveStorageFolder>> GetArchiveStorageFoldersAsync(
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
