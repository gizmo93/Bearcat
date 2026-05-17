using Bearcat.Domain.UseCases.ManageArchives.Dto;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveReadRepository
{
    Task<ArchiveDto?> GetByIdAsync(int archiveId, CancellationToken cancellationToken = default);
}
