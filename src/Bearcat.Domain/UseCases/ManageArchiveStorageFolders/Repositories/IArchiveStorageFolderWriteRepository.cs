using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Repositories;

public interface IArchiveStorageFolderWriteRepository
{
    Task<ArchiveStorageFolder> GetByIdAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken
    );

    Task<bool> NameExistsAsync(
        string name,
        int? excludedArchiveStorageFolderId,
        CancellationToken cancellationToken
    );

    Task<bool> PathExistsAsync(
        string path,
        int? excludedArchiveStorageFolderId,
        CancellationToken cancellationToken
    );

    Task<int> GetStoredArchiveCountAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken
    );

    void Add(ArchiveStorageFolder archiveStorageFolder);

    void Remove(ArchiveStorageFolder archiveStorageFolder);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
