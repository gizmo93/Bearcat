using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadFilesRepository
{
    Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Upload>> GetOrphanedUploadsAsync(CancellationToken cancellationToken);
}
