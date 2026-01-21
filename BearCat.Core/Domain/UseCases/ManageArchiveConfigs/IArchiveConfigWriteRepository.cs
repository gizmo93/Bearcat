using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageArchiveConfigs;

public interface IArchiveConfigWriteRepository
{
    void Add(ArchiveConfig archiveConfig);
    void Remove(ArchiveConfig archiveConfig);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ArchiveConfig?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
