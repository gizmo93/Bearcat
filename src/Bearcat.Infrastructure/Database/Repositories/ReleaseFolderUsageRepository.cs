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
        return await dbWrite
            .Releases.Where(release =>
                release.ReleaseFolderPath != null
                && releaseFolderPaths.Contains(release.ReleaseFolderPath)
            )
            .Select(release => release.ReleaseFolderPath!)
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingArchiveFolderPathsAsync(
        IReadOnlyList<string> archiveFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .Releases.Where(release => release.ReleaseType == ReleaseType.Unmanaged)
            .SelectMany(release => release.ArchiveConfigs)
            .Select(archiveConfig => archiveConfig.ArchiveFilesBasePath)
            .Where(archiveFolderPath => archiveFolderPaths.Contains(archiveFolderPath))
            .ToHashSetAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetRemoteDownloadFolderPathsAsync(
        IReadOnlyList<string> localFolderPaths,
        CancellationToken cancellationToken = default
    )
    {
        return await dbWrite
            .RemoteSourceDownloads.Where(download =>
                localFolderPaths.Contains(download.LocalFolderPath)
            )
            .Select(download => download.LocalFolderPath)
            .ToHashSetAsync(cancellationToken);
    }
}
