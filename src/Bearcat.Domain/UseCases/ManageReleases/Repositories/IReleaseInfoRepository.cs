using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IReleaseInfoRepository
{
    Task<
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>
    > GetActiveNfoDatabaseRegistrationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Release>> GetReleasesWithoutInfoAsync(
        int count,
        DateTime lastCheckedThreshold,
        HashSet<int> excludedReleaseIds,
        CancellationToken cancellationToken = default
    );

    Task<bool> HasReleaseInfoAsync(int releaseId, CancellationToken cancellationToken = default);

    Task<ReleaseInfo> GetReleaseInfoByIdAsync(
        int releaseInfoId,
        CancellationToken cancellationToken = default
    );

    Task<Release> GetReleaseForCoverUpdateAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<Release> GetReleaseWithInfoAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    void Remove(ReleaseInfo releaseInfo);

    void Remove(ImageUpload imageUpload);

    void DetachPendingReleaseInfo(Release release);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
