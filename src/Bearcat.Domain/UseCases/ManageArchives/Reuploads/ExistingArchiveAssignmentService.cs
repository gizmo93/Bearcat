using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileHashes;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.Reuploads;

public class ExistingArchiveAssignmentService(
    IArchiveCreationRepository repository,
    ILogger<ExistingArchiveAssignmentService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    ITransferCancellationRegistry cancellationRegistry,
    ArchiveLocalWorkingCopyService archiveLocalWorkingCopyService,
    ArchiveFileHashService archiveFileHashService,
    UploadCancellationService uploadCancellationService
)
{
    public async Task<ExistingArchiveAssignmentResult> AssignExistingArchiveAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        var assignableArchive = await repository.GetPossibleAssignableArchiveAsync(
            archiveConfigId: archiveConfig.Id,
            cancellationToken: cancellationToken
        );

        if (assignableArchive is null)
        {
            logger.LogInformation(
                "Could not find existing archive for ArchiveConfig {ArchiveConfigId}",
                archiveConfig.Id
            );

            return ExistingArchiveAssignmentResult.NewArchiveRequired;
        }

        var archiveFilesToUpload = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: assignableArchive.ArchiveFiles,
            uploads: uploads,
            uploadsOfArchive: assignableArchive.Uploads
        );

        var anyHosterOfUploadsHasCompletedUpload =
            await AnyHosterOfUploadsHasCompletedUploadOfArchiveConfigAsync(
                archiveConfig: archiveConfig,
                uploads: uploads,
                cancellationToken: cancellationToken
            );

        var archiveFilesToUploadNeedNewHashes =
            anyHosterOfUploadsHasCompletedUpload && archiveFilesToUpload.Count > 0;

        var archiver = archiverFactory.GetByName(archiveConfig.ArchiverName);

        if (archiveFilesToUploadNeedNewHashes && !archiver.CanChangeHashInPlace)
        {
            logger.LogInformation(
                "Archiver {ArchiverName} does not support changing hashes in place. Creating a new archive instead of reusing archive {ArchiveId}.",
                archiver.Name,
                assignableArchive.Id
            );

            return ExistingArchiveAssignmentResult.NewArchiveRequired;
        }

        var archiveFilesThatNeedNewHashes = GetArchiveFilesThatNeedNewHashes(
            assignableArchive: assignableArchive,
            archiveFilesToUpload: archiveFilesToUpload,
            archiveFilesToUploadNeedNewHashes: archiveFilesToUploadNeedNewHashes,
            archiver: archiver
        );

        var missingArchiveFiles = archiveFilesToUpload
            .Where(archiveFile => !fileSystemService.FileExists(archiveFile.FullFileName))
            .ToList();

        var archiveFilesToCopyIntoLocalWorkingCopy =
            ArchiveLocalWorkingCopyService.GetArchiveFilesToCopyIntoLocalWorkingCopy(
                archive: assignableArchive,
                archiveFilesToUpload: archiveFilesToUpload
            );

        if (
            missingArchiveFiles.Count > 0
            || archiveFilesThatNeedNewHashes.Count > 0
            || archiveFilesToCopyIntoLocalWorkingCopy.Count > 0
        )
        {
            var archiveHasActiveUpload = await repository.HasActiveUploadAsync(
                archiveId: assignableArchive.Id,
                cancellationToken: cancellationToken
            );

            if (archiveHasActiveUpload)
            {
                logger.LogInformation(
                    "Existing archive {ArchiveId} is currently used by an active upload. Skipping {UploadCount} uploads until the next archive creation run",
                    assignableArchive.Id,
                    uploads.Count
                );

                return ExistingArchiveAssignmentResult.PostponedBecauseArchiveHasActiveUpload;
            }
        }

        if (missingArchiveFiles.Count > 0)
        {
            await MarkArchiveFilesMissingAndNotifyAsync(
                archive: assignableArchive,
                missingArchiveFiles: missingArchiveFiles,
                waitingUploads: uploads,
                cancellationToken: cancellationToken
            );

            return ExistingArchiveAssignmentResult.ArchiveFilesMissing;
        }

        if (archiveFilesToCopyIntoLocalWorkingCopy.Count > 0)
        {
            await archiveLocalWorkingCopyService.CopyArchiveFilesIntoLocalWorkingCopyAsync(
                archive: assignableArchive,
                archiveFilesToCopy: archiveFilesToCopyIntoLocalWorkingCopy,
                cancellationToken: cancellationToken
            );
        }

        if (archiveFilesThatNeedNewHashes.Count > 0)
        {
            var hashesWereChanged =
                await AppendNullBytesToArchiveFilesOrCancelWaitingUploadsOnUserCancellationAsync(
                    archive: assignableArchive,
                    archiveFiles: archiveFilesThatNeedNewHashes,
                    waitingUploads: uploads,
                    cancellationToken: cancellationToken
                );

            if (!hashesWereChanged)
            {
                return ExistingArchiveAssignmentResult.HashChangeCanceledByUser;
            }
        }

        await AssignArchiveToUploadsAndCopyOnlineUploadedFilesAsync(
            assignableArchive: assignableArchive,
            uploads: uploads,
            cancellationToken: cancellationToken
        );

        return ExistingArchiveAssignmentResult.Assigned;
    }

    private async Task<bool> AnyHosterOfUploadsHasCompletedUploadOfArchiveConfigAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var hosterClassName in uploads
                .Select(u => u.GetHosterClassName())
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (
                await repository.HasCompletedUploadForHosterAsync(
                    archiveConfigId: archiveConfig.Id,
                    hosterClassName: hosterClassName,
                    cancellationToken: cancellationToken
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static List<ArchiveFile> GetArchiveFilesThatNeedNewHashes(
        Archive assignableArchive,
        IReadOnlyList<ArchiveFile> archiveFilesToUpload,
        bool archiveFilesToUploadNeedNewHashes,
        IArchiver archiver
    )
    {
        if (!archiver.CanChangeHashInPlace)
        {
            return [];
        }

        return assignableArchive
            .ArchiveFiles.Where(archiveFile =>
                (archiveFilesToUploadNeedNewHashes && archiveFilesToUpload.Contains(archiveFile))
                || (
                    archiveFile.Md5Hash is null
                    && !ArchiveLocalWorkingCopyService.ArchiveFileStaysUnchangedInStorageFolder(
                        archive: assignableArchive,
                        archiveFile: archiveFile,
                        archiveFilesToUpload: archiveFilesToUpload
                    )
                )
            )
            .ToList();
    }

    private async Task MarkArchiveFilesMissingAndNotifyAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> missingArchiveFiles,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Archive files {FilePaths} needed by the waiting uploads are missing in existing archive {ArchiveId}. Marking the archive for restore before changing any hashes",
            string.Join(", ", missingArchiveFiles.Select(archiveFile => archiveFile.FullFileName)),
            archive.Id
        );

        archive.ArchiveState = ArchiveState.MissingFiles;

        foreach (var upload in waitingUploads)
        {
            notificationService.Create(
                kind: NotificationKind.ArchiveFilesMissing,
                message: ArchiveFilesMissingNotificationMessage.Get(
                    upload.UploadConfig.Release.ReleaseType
                ),
                entity: upload,
                selector: n => n.Upload
            );
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task<bool> AppendNullBytesToArchiveFilesOrCancelWaitingUploadsOnUserCancellationAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);
        using var userCancellableTransfer = cancellationRegistry.RegisterUserCancellableTransfer(
            transferIdentifier,
            cancellationToken
        );

        try
        {
            await archiveFileHashService.AppendNullBytesUntilHashesAreUniqueAsync(
                archive: archive,
                archiveFiles: archiveFiles,
                transferType: TransferType.ArchiveHashChange,
                cancellationToken: userCancellableTransfer.CancellationToken
            );

            return true;
        }
        catch (OperationCanceledException) when (userCancellableTransfer.WasCanceledByUser)
        {
            await SaveChangedHashesAndCancelWaitingUploadsAsync(
                archive: archive,
                waitingUploads: waitingUploads,
                cancellationToken: cancellationToken
            );

            return false;
        }
        catch (OperationCanceledException)
        {
            await repository.SaveChangesAsync(cancellationToken: CancellationToken.None);

            throw;
        }
    }

    private async Task SaveChangedHashesAndCancelWaitingUploadsAsync(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Canceled the MD5 hash change of existing archive {ArchiveId} on user request. {FileCountWithoutHash} archive files have no valid hash anymore and get new hashes before the archive is assigned again",
            archive.Id,
            archive.ArchiveFiles.Count(f => f.Md5Hash is null)
        );

        CancelUploadsBecauseArchiveCreationWasCanceled(waitingUploads);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private void CancelUploadsBecauseArchiveCreationWasCanceled(
        IReadOnlyList<Upload> waitingUploads
    )
    {
        uploadCancellationService.CancelUploadsAndNotify(
            waitingUploads,
            "Upload canceled because the archive creation was canceled."
        );

        logger.LogInformation(
            "Canceled {UploadCount} uploads that were waiting for the canceled archive creation",
            waitingUploads.Count
        );
    }

    private async Task AssignArchiveToUploadsAndCopyOnlineUploadedFilesAsync(
        Archive assignableArchive,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        foreach (var upload in uploads)
        {
            upload.ArchiveId = assignableArchive.Id;
            upload.UploadState = UploadState.Pending;

            CopyOnlineUploadedFilesFromPreviousUploads(upload, assignableArchive);
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Assigned existing archive {ArchiveId} to {UploadCount} uploads",
            assignableArchive.Id,
            uploads.Count
        );
    }

    private void CopyOnlineUploadedFilesFromPreviousUploads(
        Upload newUpload,
        Archive assignableArchive
    )
    {
        var reusableOnlineFiles = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: newUpload,
            uploadsOfArchive: assignableArchive.Uploads
        );

        if (reusableOnlineFiles.Count == 0)
        {
            return;
        }

        newUpload.UploadedFiles = reusableOnlineFiles
            .Select(source => new UploadedFile
            {
                Upload = newUpload,
                ArchiveFileId = source.ArchiveFileId,
                HosterFileLink = source.HosterFileLink,
                ExternalId = source.ExternalId,
                HosterFolderId = source.HosterFolderId,
                Md5Hash = source.Md5Hash,
                OnlineState = OnlineState.Online,
                CreatedAt = source.CreatedAt,
                CheckedAt = source.CheckedAt,
            })
            .ToList();

        logger.LogInformation(
            "Carried over {FileCount} online files to reupload {UploadId} from previous uploads of archive {ArchiveId}",
            newUpload.UploadedFiles.Count,
            newUpload.Id,
            assignableArchive.Id
        );
    }
}
