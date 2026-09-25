using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs;

public interface IArchiveConfigWriteRepository
{
    void Add(ArchiveConfig archiveConfig);
    void Remove(ArchiveConfig archiveConfig);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<ArchiveConfig?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdditionalArchiveContent>> GetAdditionalArchiveContentsAsync(
        IReadOnlyList<int> additionalArchiveContentIds,
        CancellationToken cancellationToken = default
    );
}
