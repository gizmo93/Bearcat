using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Search;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveReadRepository
{
    Task<ArchiveReadModel?> GetByIdAsync(
        int archiveId,
        CancellationToken cancellationToken = default
    );

    Task<PagedResult<ArchiveListItemReadModel>> SearchArchivesAsync(
        ArchiveSearchQuery query,
        CancellationToken cancellationToken = default
    );

    Task<
        IReadOnlyList<RunningArchiveReadModel>
    > GetCreatingOrRestoringArchivesOrArchivesWithIdsAsync(
        IReadOnlyList<int> archiveIds,
        CancellationToken cancellationToken = default
    );
}
