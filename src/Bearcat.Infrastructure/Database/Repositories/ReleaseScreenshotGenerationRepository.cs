using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseScreenshotGenerationRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseScreenshotGenerationRepository
{
    public async Task<List<int>> GetReleaseIdsPendingScreenshotGenerationAsync(
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Releases.Where(release =>
                release.ReleaseType == ReleaseType.Managed
                && release.ReleaseFolderPath != null
                && (release.CreateScreenshots || release.CreateScreenshotGrid)
                && release.MediaFiles.Any()
                && release.ScreenshotsGeneratedAt == null
                && release.ScreenshotGenerationErrorMessage == null
            )
            .OrderBy(release => release.Id)
            .Select(release => release.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Release?> GetReleaseWithMediaFilesAndScreenshotsAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite
            .Releases.AsSplitQuery()
            .Include(release => release.MediaFiles)
            .Include(release => release.Screenshots)
            .FirstOrDefaultAsync(release => release.Id == releaseId, cancellationToken);
    }

    public void RemoveScreenshots(IReadOnlyList<ReleaseScreenshot> screenshots)
    {
        dbWrite.ReleaseScreenshots.RemoveRange(screenshots);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
