using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders.Repositories;

public interface ILocalFolderScanRepository
{
    void Add(Release release);

    void Add(Notification notification);

    Task<IReadOnlyList<ReleaseFolderAutomation>> GetEnabledWithTemplatesAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ReleaseFolderObservation>> GetFolderObservationsAsync(
        CancellationToken cancellationToken = default
    );

    void AddFolderObservation(ReleaseFolderObservation observation);

    Task<IReadOnlyList<ReleaseFolderAutomationIgnoredFolder>> GetIgnoredFoldersAsync(
        IReadOnlyList<int> releaseFolderAutomationIds,
        CancellationToken cancellationToken = default
    );

    void AddIgnoredFolder(ReleaseFolderAutomationIgnoredFolder ignoredFolder);

    void RemoveFolderObservation(ReleaseFolderObservation observation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
