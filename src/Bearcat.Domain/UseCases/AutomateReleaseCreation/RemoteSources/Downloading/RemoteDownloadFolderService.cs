using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteDownloadFolderService(
    IFileSystemService fileSystemService,
    ILogger<RemoteDownloadFolderService> logger
)
{
    public bool LocalFolderHasEntries(RemoteSourceDownload download)
    {
        return fileSystemService.DirectoryHasEntries(download.LocalFolderPath);
    }

    public bool DeleteLocalFolder(RemoteSourceDownload download)
    {
        if (!RemoteDownloadPaths.CanDeleteDownloadFolder(download))
        {
            logger.LogWarning(
                "Not deleting {LocalFolderPath} of remote download {DownloadId} because its folder name {FolderName} is unsafe or differs from the last path segment",
                download.LocalFolderPath,
                download.Id,
                download.FolderName
            );

            return false;
        }

        try
        {
            fileSystemService.DeleteDirectoryIfExists(download.LocalFolderPath);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Could not delete {LocalFolderPath} of remote download {DownloadId}",
                download.LocalFolderPath,
                download.Id
            );

            return false;
        }
    }
}
