using Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;

public interface IReleaseScreenshotReadRepository
{
    Task<ReleaseScreenshotOverviewReadModel> GetOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    Task<string?> GetRelativeFilePathAsync(
        int releaseScreenshotId,
        CancellationToken cancellationToken = default
    );
}
