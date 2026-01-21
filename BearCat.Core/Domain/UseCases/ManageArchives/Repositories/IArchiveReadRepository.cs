using BearCat.Core.Domain.UseCases.ManageArchives.Dto;

namespace BearCat.Core.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveReadRepository
{
    Task<ArchiveDto?> GetByIdAsync(
        int archiveId,
        CancellationToken cancellationToken = default);
}
