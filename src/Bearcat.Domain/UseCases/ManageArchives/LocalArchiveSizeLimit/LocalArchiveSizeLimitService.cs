using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.LocalArchiveSizeLimit;

public class LocalArchiveSizeLimitService(
    IArchiveCleanupRepository repository,
    IApplicationConfigurationProvider configuration,
    IFileSystemService fileSystemService,
    MirrorCoverageEvaluator mirrorCoverageEvaluator,
    LocalArchiveDeleter localArchiveDeleter,
    INotificationService notificationService,
    ILogger<LocalArchiveSizeLimitService> logger
)
{
    public async Task DeleteOldestArchivesToFitAsync(
        long requiredBytes,
        CancellationToken cancellationToken
    )
    {
        var maximumLocalArchiveSizeGb = configuration.GetValue<ArchiveCleanupConfiguration>(c =>
            c.MaximumLocalArchiveSizeGb
        );

        var retentionAction = configuration.GetValue<
            ArchiveCleanupConfiguration,
            ArchiveRetentionAction
        >(c => c.ArchiveRetentionAction);

        if (
            maximumLocalArchiveSizeGb == 0
            || retentionAction is ArchiveRetentionAction.MoveToStorageFolder
        )
        {
            return;
        }

        var maximumLocalArchiveBytes =
            maximumLocalArchiveSizeGb * ArchiveStorageFolderSelector.BytesPerGigabyte;

        var localArchiveFilePaths = await repository.GetLocalCreatedArchiveFilePathsAsync(
            cancellationToken
        );

        var localArchiveBytes = GetSizeOfExistingFilesBytes(localArchiveFilePaths);

        if (localArchiveBytes + requiredBytes <= maximumLocalArchiveBytes)
        {
            return;
        }

        var deletableArchives =
            await repository.GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync(
                cancellationToken
            );

        foreach (var archive in deletableArchives)
        {
            if (localArchiveBytes + requiredBytes <= maximumLocalArchiveBytes)
            {
                break;
            }

            if (!mirrorCoverageEvaluator.IsArchiveRecoverable(archive))
            {
                continue;
            }

            var archiveBytes = GetSizeOfExistingFilesBytes(
                archive.ArchiveFiles.Select(archiveFile => archiveFile.FullFileName).ToList()
            );

            try
            {
                localArchiveDeleter.DeleteLocalArchive(archive);
                localArchiveBytes -= archiveBytes;

                logger.LogInformation(
                    "Deleted archive {ArchiveId} at {ArchiveFolderPath} to stay below the maximum size of local archives of {MaximumLocalArchiveSizeGb} GB",
                    archive.Id,
                    archive.ArchiveFolderPath,
                    maximumLocalArchiveSizeGb
                );
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not delete archive {ArchiveId} at {ArchiveFolderPath}",
                    archive.Id,
                    archive.ArchiveFolderPath
                );
            }
        }

        if (localArchiveBytes + requiredBytes > maximumLocalArchiveBytes)
        {
            await ReportLimitExceededAsync(
                localArchiveBytes: localArchiveBytes,
                requiredBytes: requiredBytes,
                maximumLocalArchiveSizeGb: maximumLocalArchiveSizeGb,
                cancellationToken: cancellationToken
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private long GetSizeOfExistingFilesBytes(IReadOnlyList<string> filePaths)
    {
        return filePaths
            .Where(fileSystemService.FileExists)
            .Sum(fileSystemService.GetFileSizeBytes);
    }

    private async Task ReportLimitExceededAsync(
        long localArchiveBytes,
        long requiredBytes,
        int maximumLocalArchiveSizeGb,
        CancellationToken cancellationToken
    )
    {
        var localArchiveSize = localArchiveBytes
            .Bytes()
            .Humanize("0.0", CultureInfo.InvariantCulture);
        var requiredSize = requiredBytes.Bytes().Humanize("0.0", CultureInfo.InvariantCulture);

        logger.LogWarning(
            "Local archives use {LocalArchiveSize} and the next archive needs {RequiredSize}, which exceeds the maximum size of local archives of {MaximumLocalArchiveSizeGb} GB because no further archive can be deleted",
            localArchiveSize,
            requiredSize,
            maximumLocalArchiveSizeGb
        );

        var hasUnresolvedNotification = await repository.HasUnresolvedNotificationAsync(
            NotificationKind.LocalArchiveSizeLimitExceeded,
            cancellationToken
        );

        if (hasUnresolvedNotification)
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.LocalArchiveSizeLimitExceeded,
            message: $"Local archives use {localArchiveSize} and the next archive needs {requiredSize}, which exceeds the maximum size of local archives of {maximumLocalArchiveSizeGb} GB. No further local archive can be deleted because the remaining ones have no online mirror or release folder, are excluded from auto cleanup or are used by an upload. The archive is written anyway."
        );
    }
}
