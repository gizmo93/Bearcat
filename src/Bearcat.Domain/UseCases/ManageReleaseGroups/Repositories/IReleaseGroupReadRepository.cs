using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;

public interface IReleaseGroupReadRepository
{
    Task<IReadOnlyList<ReleaseGroupReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<ReleaseGroupReadModel?> GetReadModelByIdAsync(
        int releaseGroupId,
        CancellationToken cancellationToken = default
    );
}
