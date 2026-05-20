using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadStateRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsToCheckAsync(
        DateTime localNow,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        DateTime releaseCreatedBefore,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetUploadsEligibleForAutomaticReuploadAsync(
        CancellationToken cancellationToken
    );

    Task<Upload> GetUploadForReuploadAsync(int uploadId, CancellationToken cancellationToken);

    Task<Upload?> GetByIdAsync(int uploadId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Upload upload);

    void Remove(Upload upload);
}
