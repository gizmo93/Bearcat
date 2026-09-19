using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseClassificationRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseClassificationRepository
{
    public async Task<Release?> GetReleaseForClassificationAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.MediaFiles)
            .Include(release => release.Classification)
            .Include(release => release.ReleaseInfo)
            .Include(release => release.ReleaseNfo)
            .FirstOrDefaultAsync(release => release.Id == releaseId, cancellationToken);
    }

    public async Task<IReadOnlyList<Release>> GetReleasesNeedingClassificationAsync(
        int count,
        int parserVersion,
        HashSet<int> excludedReleaseIds,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.MediaFiles)
            .Include(release => release.Classification)
            .Include(release => release.ReleaseInfo)
            .Include(release => release.ReleaseNfo)
            .Where(release => !excludedReleaseIds.Contains(release.Id))
            .Where(release =>
                release.Classification == null
                || release.Classification.ParserVersion < parserVersion
            )
            .OrderBy(release => release.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public void ClearChangeTracker()
    {
        dbWrite.ChangeTracker.Clear();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
