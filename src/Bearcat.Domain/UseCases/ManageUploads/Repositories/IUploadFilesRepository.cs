using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadFilesRepository
{
    Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(
        IReadOnlySet<int> uploadIdsToExclude,
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Upload>> GetOrphanedUploadsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetCancellationRequestedUploadIdsAsync(
        CancellationToken cancellationToken
    );
    Task<bool> IsCancellationRequestedAsync(int uploadId, CancellationToken cancellationToken);
    Task<Upload?> GetUploadByIdAsync(int uploadId, CancellationToken cancellationToken);
    void ClearChangeTracker();

    Task<IReadOnlyDictionary<int, string>> GetConfigByHosterRegistrationId(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, HosterUploadConcurrencyInfo>> GetConfigByHosterClassName(
        CancellationToken cancellationToken
    );
}
