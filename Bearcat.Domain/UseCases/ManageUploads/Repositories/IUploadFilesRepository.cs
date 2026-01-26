using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadFilesRepository
{
    Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(
        IReadOnlySet<int> uploadIdsToExclude,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Upload>> GetOrphanedUploadsAsync(CancellationToken cancellationToken);
    void ClearChangeTracker();
}
