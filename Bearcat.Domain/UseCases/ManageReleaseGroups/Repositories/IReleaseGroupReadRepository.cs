using Bearcat.Domain.UseCases.ManageReleaseGroups.Dto;

namespace Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;

public interface IReleaseGroupReadRepository
{
    Task<IReadOnlyList<ReleaseGroupDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ReleaseGroupDto?> GetReadModelByIdAsync(
        int releaseGroupId,
        CancellationToken cancellationToken = default
    );
}
