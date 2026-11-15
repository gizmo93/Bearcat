using Bearcat.Domain.UseCases.ManageReleaseScreenshots.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseScreenshotReadRepository(IBearcatReadDbContext dbRead)
    : IReleaseScreenshotReadRepository
{
    public async Task<ReleaseScreenshotOverviewReadModel> GetOverviewAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .Releases.Where(release => release.Id == releaseId)
            .Select(release => new ReleaseScreenshotOverviewReadModel(
                release.ReleaseType,
                release.ReleaseFolderPath != null,
                release.CreateScreenshots,
                release.CreateScreenshotGrid,
                release.MediaFiles.Any(),
                release.MediaMetadataExtractedAt,
                release.ScreenshotsGeneratedAt,
                release.ScreenshotGenerationErrorMessage,
                release
                    .Screenshots.OrderBy(screenshot => screenshot.Kind)
                    .ThenBy(screenshot => screenshot.Index)
                    .Select(screenshot => new ReleaseScreenshotReadModel(
                        screenshot.Id,
                        screenshot.Kind,
                        screenshot.Index,
                        screenshot.Timestamp,
                        screenshot.Width,
                        screenshot.Height,
                        screenshot.SizeBytes
                    ))
                    .ToList()
            ))
            .FirstAsync(cancellationToken);
    }

    public async Task<string?> GetRelativeFilePathAsync(
        int releaseScreenshotId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbRead
            .ReleaseScreenshots.Where(screenshot => screenshot.Id == releaseScreenshotId)
            .Select(screenshot => screenshot.RelativeFilePath)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
