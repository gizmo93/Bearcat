using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveCleanupRepository
{
    Task<IReadOnlyList<Archive>> GetDeletableArchivesAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
