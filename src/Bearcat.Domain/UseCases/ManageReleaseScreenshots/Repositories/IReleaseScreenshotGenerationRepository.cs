using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;

public interface IReleaseScreenshotGenerationRepository
{
    Task<List<int>> GetReleaseIdsPendingScreenshotGenerationAsync(
        CancellationToken cancellationToken
    );

    Task<Release?> GetReleaseWithMediaFilesAndScreenshotsAsync(
        int releaseId,
        CancellationToken cancellationToken
    );

    void RemoveScreenshots(IReadOnlyList<ReleaseScreenshot> screenshots);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
