using Bearcat.Domain.UseCases.ManageArchives.ReadModels;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveReadRepository
{
    Task<ArchiveReadModel?> GetByIdAsync(
        int archiveId,
        CancellationToken cancellationToken = default
    );

    Task<
        IReadOnlyList<RunningArchiveReadModel>
    > GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken = default
    );
}
