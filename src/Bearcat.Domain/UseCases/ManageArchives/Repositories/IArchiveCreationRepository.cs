using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.Repositories;

public interface IArchiveCreationRepository
{
    Task<IReadOnlyList<Upload>> GetUploadsWithoutArchiveAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void Add(Archive archive);
    Task<Archive?> GetPossibleAssignableArchiveAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    );
    Task<bool> HasCompletedUploadForHosterAsync(
        int archiveConfigId,
        string hosterClassName,
        CancellationToken cancellationToken
    );
    Task<bool> HasActiveUploadAsync(int archiveId, CancellationToken cancellationToken);
    Task<int?> GetLastArchiveFileSizeMbAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    );
    Task DeleteOrphanedArchivesAsync(CancellationToken cancellationToken);
}
