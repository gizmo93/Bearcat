using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases.Repositories;

public interface IAutomaticallyCreateReleasesRepository
{
    void Add(Release release);

    void Add(Notification notification);

    Task<HashSet<string>> GetExistingReleaseFolderPathsAsync(
        IReadOnlyCollection<string> releaseFolderPaths,
        CancellationToken cancellationToken = default
    );

    Task<HashSet<string>> GetExistingArchiveFolderPathsAsync(
        IReadOnlyCollection<string> archiveFolderPaths,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseFolderAutomation>> GetEnabledWithTemplatesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseFolderObservation>> GetFolderObservationsAsync(
        CancellationToken cancellationToken = default
    );

    void AddFolderObservation(ReleaseFolderObservation observation);

    void RemoveFolderObservation(ReleaseFolderObservation observation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
