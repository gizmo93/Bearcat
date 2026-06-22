using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IMediaMetadataRepository
{
    Task<Release?> GetReleaseWithMediaFilesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    );

    void RemoveMediaFile(ReleaseMediaFile mediaFile);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
