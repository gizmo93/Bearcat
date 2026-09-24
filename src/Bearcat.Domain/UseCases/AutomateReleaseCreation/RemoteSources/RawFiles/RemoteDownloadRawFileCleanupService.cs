using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles;

public class RemoteDownloadRawFileCleanupService(
    IRemoteDownloadRawFileCleanupRepository repository,
    UnmanagedReleaseConverter unmanagedReleaseConverter,
    RemoteDownloadFolderService folderService,
    INotificationService notificationService,
    ILogger<RemoteDownloadRawFileCleanupService> logger
)
{
    private const string ConversionReason =
        "The release was converted to unmanaged because every upload config has completed its first upload.";

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var downloads = await repository.GetRawFileCleanupCandidatesAsync(cancellationToken);

        foreach (var download in downloads)
        {
            var release = download.Release!;

            if (!CanDeleteRawFiles(release))
            {
                logger.LogDebug(
                    "Keeping raw files of remote download {DownloadId} because release {ReleaseId} is not fully uploaded yet or not recoverable without its release folder",
                    download.Id,
                    release.Id
                );

                continue;
            }

            await ConvertToUnmanagedAndDeleteRawFilesAsync(download, release, cancellationToken);
        }
    }

    private bool CanDeleteRawFiles(Release release)
    {
        var uploads = release.UploadConfigs.SelectMany(config => config.Uploads).ToList();
        var archives = release.ArchiveConfigs.SelectMany(config => config.Archives).ToList();

        return release.UploadConfigs.Count > 0
            && release.UploadConfigs.All(config =>
                config.Uploads.Any(upload => upload.UploadState is UploadState.Completed)
            )
            && !uploads.Any(upload =>
                upload.UploadState
                    is UploadState.WaitingForArchive
                        or UploadState.Pending
                        or UploadState.Uploading
                        or UploadState.CancellationRequested
            )
            && !archives.Any(archive => archive.ArchiveState is ArchiveState.Creating)
            && unmanagedReleaseConverter.IsRecoverableWithoutReleaseFolder(release);
    }

    private async Task ConvertToUnmanagedAndDeleteRawFilesAsync(
        RemoteSourceDownload download,
        Release release,
        CancellationToken cancellationToken
    )
    {
        var rawFolderPath = download.LocalFolderPath;
        var hasArchivesInsideRawFolder = UnmanagedReleaseConverter.HasCreatedArchiveInside(
            release,
            rawFolderPath
        );

        UnmanagedReleaseConverter.ConvertToUnmanaged(release);
        await repository.SaveChangesAsync(cancellationToken);

        var rawFilesDeleted =
            !hasArchivesInsideRawFolder && folderService.DeleteLocalFolder(download);

        notificationService.Create(
            kind: NotificationKind.ReleaseAutoConvertedToUnmanaged,
            message: GetNotificationMessage(
                rawFolderPath,
                hasArchivesInsideRawFolder,
                rawFilesDeleted
            ),
            entity: release,
            selector: notification => notification.Release
        );
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Converted release {ReleaseId} of remote download {DownloadId} to unmanaged after every upload config completed its first upload, raw files in {RawFolderPath} kept: {RawFilesKept}",
            release.Id,
            download.Id,
            rawFolderPath,
            hasArchivesInsideRawFolder
        );
    }

    private static string GetNotificationMessage(
        string rawFolderPath,
        bool hasArchivesInsideRawFolder,
        bool rawFilesDeleted
    )
    {
        if (hasArchivesInsideRawFolder)
        {
            return $"{ConversionReason} The raw files in {rawFolderPath} were kept because the archives of this release are stored inside this folder. Delete only the release data there and keep the archive files.";
        }

        if (rawFilesDeleted)
        {
            return $"{ConversionReason} The raw files in {rawFolderPath} were deleted.";
        }

        return $"{ConversionReason} The raw files in {rawFolderPath} could not be deleted, so delete them yourself.";
    }
}
