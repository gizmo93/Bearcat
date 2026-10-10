using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ArchiveStorageFolderRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite
) : IArchiveStorageFolderReadRepository, IArchiveStorageFolderWriteRepository
{
    public async Task<IReadOnlyList<ArchiveStorageFolderReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ArchiveStorageFolders.OrderBy(storageFolder => storageFolder.Priority)
            .ThenBy(storageFolder => storageFolder.Name)
            .Select(storageFolder => new ArchiveStorageFolderReadModel(
                storageFolder.Id,
                storageFolder.Name,
                storageFolder.Path,
                storageFolder.IsActive,
                storageFolder.MinimumFreeSpaceGb,
                storageFolder.Priority,
                storageFolder.RetrieveArchivesBeforeReupload,
                dbRead.Archives.Count(archive => archive.ArchiveStorageFolderId == storageFolder.Id)
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<ArchiveStorageFolder> GetByIdAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.ArchiveStorageFolders.FirstAsync(
            storageFolder => storageFolder.Id == archiveStorageFolderId,
            cancellationToken
        );
    }

    public async Task<bool> NameExistsAsync(
        string name,
        int? excludedArchiveStorageFolderId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.ArchiveStorageFolders.AnyAsync(
            storageFolder =>
                storageFolder.Name == name && storageFolder.Id != excludedArchiveStorageFolderId,
            cancellationToken
        );
    }

    public async Task<bool> PathExistsAsync(
        string path,
        int? excludedArchiveStorageFolderId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.ArchiveStorageFolders.AnyAsync(
            storageFolder =>
                storageFolder.Path == path && storageFolder.Id != excludedArchiveStorageFolderId,
            cancellationToken
        );
    }

    public async Task<int> GetStoredArchiveCountAsync(
        int archiveStorageFolderId,
        CancellationToken cancellationToken
    )
    {
        return await dbRead.Archives.CountAsync(
            archive => archive.ArchiveStorageFolderId == archiveStorageFolderId,
            cancellationToken
        );
    }

    public void Add(ArchiveStorageFolder archiveStorageFolder)
    {
        dbWrite.Add(archiveStorageFolder);
    }

    public void Remove(ArchiveStorageFolder archiveStorageFolder)
    {
        dbWrite.Remove(archiveStorageFolder);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
