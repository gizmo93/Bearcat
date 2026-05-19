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

    Task<IReadOnlyList<ReleaseFolderAutomation>> GetEnabledWithTemplatesAsync(
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
