using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class ReleaseFolderUsageRepository(IBearcatWriteDbContext dbWrite)
    : IReleaseFolderUsageRepository
{
    public async Task<HashSet<string>> GetExistingReleaseFolderPathsAsync(
        IReadOnlyList<string> releaseFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await QueryExistingReleaseFolderPaths(releaseFolderPaths)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingArchiveFolderPathsAsync(
        IReadOnlyList<string> archiveFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await QueryExistingArchiveFolderPathsOfUnmanagedReleases(archiveFolderPaths)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetRemoteDownloadFolderPathsAsync(
        IReadOnlyList<string> localFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await QueryRemoteDownloadFolderPaths(localFolderPaths)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
        IReadOnlyList<string> folderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await QueryExistingReleaseFolderPaths(folderPaths)
            .Union(QueryExistingArchiveFolderPathsOfUnmanagedReleases(folderPaths))
            .Union(QueryRemoteDownloadFolderPaths(folderPaths))
            .ToHashSetAsync(cancellationToken);
    }

    private IQueryable<string> QueryExistingReleaseFolderPaths(
        IReadOnlyList<string> releaseFolderPaths
    )
    {
        return dbWrite
            .Releases.Where(release =>
                release.ReleaseFolderPath != null
                && EF.Parameter(releaseFolderPaths).Contains(release.ReleaseFolderPath)
            )
            .Select(release => release.ReleaseFolderPath!);
    }

    private IQueryable<string> QueryExistingArchiveFolderPathsOfUnmanagedReleases(
        IReadOnlyList<string> archiveFolderPaths
    )
    {
        return dbWrite
            .Releases.Where(release => release.ReleaseType == ReleaseType.Unmanaged)
            .SelectMany(release => release.ArchiveConfigs)
            .Select(archiveConfig => archiveConfig.ArchiveFilesBasePath)
            .Where(archiveFolderPath =>
                EF.Parameter(archiveFolderPaths).Contains(archiveFolderPath)
            );
    }

    private IQueryable<string> QueryRemoteDownloadFolderPaths(
        IReadOnlyList<string> localFolderPaths
    )
    {
        return dbWrite
            .RemoteSourceDownloads.Where(download =>
                EF.Parameter(localFolderPaths).Contains(download.LocalFolderPath)
            )
            .Select(download => download.LocalFolderPath);
    }
}
