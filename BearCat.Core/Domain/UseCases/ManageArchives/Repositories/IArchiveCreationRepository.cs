using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveCreationRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsWithoutArchiveAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Archive archive);
    Task<int?> GetPossibleAssignableArchiveId(int archiveConfigId, CancellationToken cancellationToken);
    Task DeleteOrphanedArchivesAsync(CancellationToken cancellationToken);
}
