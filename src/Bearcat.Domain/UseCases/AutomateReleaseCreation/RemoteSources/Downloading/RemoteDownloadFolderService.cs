using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public class RemoteDownloadFolderService(
    IFileSystemService fileSystemService,
    ILogger<RemoteDownloadFolderService> logger
)
{
    public bool ContainsData(RemoteSourceDownload download)
    {
        return fileSystemService.DirectoryHasEntries(download.LocalFolderPath);
    }

    public void DeleteDownloadedFiles(RemoteSourceDownload download)
    {
        if (!RemoteDownloadPaths.CanDeleteDownloadFolder(download))
        {
            logger.LogWarning(
                "Not deleting {LocalFolderPath} of remote download {DownloadId} because it is not a download folder inside the target path of its automation",
                download.LocalFolderPath,
                download.Id
            );

            return;
        }

        try
        {
            fileSystemService.DeleteDirectoryIfExists(download.LocalFolderPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Could not delete {LocalFolderPath} of remote download {DownloadId}",
                download.LocalFolderPath,
                download.Id
            );
        }
    }
}
