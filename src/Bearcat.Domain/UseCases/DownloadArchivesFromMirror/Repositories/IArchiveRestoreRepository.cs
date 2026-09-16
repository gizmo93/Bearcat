using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;

public interface IArchiveRestoreRepository
{
    Task<IReadOnlyList<Archive>> GetInterruptedRestoresAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Upload>> GetUploadsWaitingForRestoreAsync(
        CancellationToken cancellationToken
    );

    Task<Archive?> GetLatestArchiveAsync(int archiveConfigId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Upload>> GetUploadsOfArchiveAsync(
        int archiveId,
        CancellationToken cancellationToken
    );

    Task<string> GetSerializedConfigAsync(
        int hosterRegistrationId,
        CancellationToken cancellationToken
    );

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
