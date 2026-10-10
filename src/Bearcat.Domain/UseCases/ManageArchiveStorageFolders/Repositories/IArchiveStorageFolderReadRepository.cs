using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;

namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Repositories;

public interface IArchiveStorageFolderReadRepository
{
    Task<IReadOnlyList<ArchiveStorageFolderReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
