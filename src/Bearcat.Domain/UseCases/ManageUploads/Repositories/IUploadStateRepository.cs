using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadStateRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsToCheckAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetUploadsEligibleForAutomaticReuploadAsync(
        CancellationToken cancellationToken
    );

    Task<Upload> GetUploadForReuploadAsync(int uploadId, CancellationToken cancellationToken);

    Task<Upload> GetUploadForCancellationAsync(int uploadId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Upload upload);
}
