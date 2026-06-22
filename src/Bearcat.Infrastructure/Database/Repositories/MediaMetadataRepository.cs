using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class MediaMetadataRepository(IBearcatWriteDbContext dbWrite) : IMediaMetadataRepository
{
    public async Task<Release?> GetReleaseWithMediaFilesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Include(release => release.MediaFiles)
            .FirstOrDefaultAsync(release => release.Id == releaseId, cancellationToken);
    }

    public void RemoveMediaFile(ReleaseMediaFile mediaFile)
    {
        dbWrite.Remove(mediaFile);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
