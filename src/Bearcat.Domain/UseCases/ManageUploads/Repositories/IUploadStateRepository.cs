using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public interface IUploadStateRepository
{
    Task<IReadOnlyList<HosterRegistration>> GetActiveHosterRegistrationsAsync(
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetLeastRecentlyCheckedUploadsToCheckAsync(
        int hosterRegistrationId,
        DateTime localNow,
        int maximumUploadCount,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetUploadsOfReleasesToCheckOnlineStateAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsAsync(
        DateTime releaseCreatedBefore,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<UploadConfig>> GetUploadConfigsWithoutUploadsOfReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<Upload>> GetUploadsDueForAutomaticReuploadAsync(
        DateTime localNow,
        int maximumUploadCount,
        CancellationToken cancellationToken
    );

    Task<Upload> GetUploadForReuploadAsync(int uploadId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Upload>> GetLatestUploadOfEachUploadConfigOfReleasesForReuploadAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken
    );

    Task<Upload?> GetUploadForOnlineCheckAsync(int uploadId, CancellationToken cancellationToken);

    Task<Upload?> GetByIdAsync(int uploadId, CancellationToken cancellationToken);

    Task<bool> HasUnresolvedNotificationForHosterRegistrationAsync(
        int hosterRegistrationId,
        NotificationKind kind,
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Upload upload);

    void Remove(Upload upload);
}
