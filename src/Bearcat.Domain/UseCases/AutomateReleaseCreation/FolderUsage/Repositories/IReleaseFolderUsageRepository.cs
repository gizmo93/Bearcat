namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;

public interface IReleaseFolderUsageRepository
{
    Task<HashSet<string>> GetExistingReleaseFolderPathsAsync(
        IReadOnlyList<string> releaseFolderPaths,
        CancellationToken cancellationToken = default
    );

    Task<HashSet<string>> GetExistingArchiveFolderPathsAsync(
        IReadOnlyList<string> archiveFolderPaths,
        CancellationToken cancellationToken = default
    );

    Task<HashSet<string>> GetRemoteDownloadFolderPathsAsync(
        IReadOnlyList<string> localFolderPaths,
        CancellationToken cancellationToken = default
    );
}
