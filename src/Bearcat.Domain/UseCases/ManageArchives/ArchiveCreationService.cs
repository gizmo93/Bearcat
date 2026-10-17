using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileHashes;
using Bearcat.Domain.UseCases.ManageArchives.FailedArchiveCreations;
using Bearcat.Domain.UseCases.ManageArchives.InterruptedArchives;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPacking;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.Reuploads;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveCreationService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    INotificationService notificationService,
    ITransferCancellationRegistry cancellationRegistry,
    UploadCancellationService uploadCancellationService,
    InterruptedArchiveRecoveryService interruptedArchiveRecoveryService,
    FailedArchiveCreationService failedArchiveCreationService,
    ExistingArchiveAssignmentService existingArchiveAssignmentService,
    ArchivePackingSettingsService archivePackingSettingsService,
    ReleaseFolderPackingService releaseFolderPackingService,
    ArchiveFileHashService archiveFileHashService
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await interruptedArchiveRecoveryService.RecoverOrDeleteInterruptedArchivesAsync(
            cancellationToken
        );
        await failedArchiveCreationService.FailUploadsWaitingForCreationFailedArchivesAsync(
            cancellationToken
        );
        await AssignExistingOrCreateNewArchivesForUploadsWithoutArchiveAsync(cancellationToken);
    }

    private async Task AssignExistingOrCreateNewArchivesForUploadsWithoutArchiveAsync(
        CancellationToken cancellationToken
    )
    {
        var uploadsWithoutArchive = await repository.GetUploadsWithoutArchiveAsync(
            cancellationToken
        );

        var archivesToCreate = new List<(ArchiveConfig ArchiveConfig, List<Upload> Uploads)>();

        foreach (
            var uploadsOfArchiveConfig in uploadsWithoutArchive.GroupBy(upload =>
                upload.UploadConfig.ArchiveConfig!
            )
        )
        {
            var archiveConfig = uploadsOfArchiveConfig.Key;
            var uploads = uploadsOfArchiveConfig.ToList();

            logger.LogInformation(
                "Processing uploads {UploadIds} for UploadConfig {UploadConfigIds} without archive",
                uploads.Select(u => u.Id),
                uploads.Select(u => u.UploadConfigId)
            );

            var existingArchiveAssignmentResult =
                await existingArchiveAssignmentService.AssignExistingArchiveAsync(
                    archiveConfig: archiveConfig,
                    uploads: uploads,
                    cancellationToken: cancellationToken
                );

            if (
                existingArchiveAssignmentResult
                is not ExistingArchiveAssignmentResult.NewArchiveRequired
            )
            {
                continue;
            }

            if (NewArchivesCanBeCreatedForRelease(uploads.First().UploadConfig.Release))
            {
                archivesToCreate.Add((archiveConfig, uploads));
            }
        }

        if (archivesToCreate.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Creating {ArchiveCount} new archives for uploads",
            archivesToCreate.Count
        );

        foreach (var (archiveConfig, uploads) in archivesToCreate)
        {
            await CreateAndPackNewArchiveAsync(archiveConfig, uploads, cancellationToken);
        }
    }

    private static bool NewArchivesCanBeCreatedForRelease(Release release) =>
        release.ReleaseType is ReleaseType.Managed;

    private async Task CreateAndPackNewArchiveAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Creating archive for ArchiveConfig {ArchiveConfigId} with {UploadCount} uploads and archiver {ArchiverClassName}",
            archiveConfig.Id,
            uploads.Count,
            archiveConfig.ArchiverName
        );

        var releaseFolderPath = archiveConfig.Release.ReleaseFolderPath;

        if (string.IsNullOrEmpty(releaseFolderPath) || !Directory.Exists(releaseFolderPath))
        {
            logger.LogError(
                "Release folder path {ReleaseFolderPath} does not exist for ArchiveConfig {ArchiveConfigId}",
                releaseFolderPath,
                archiveConfig.Id
            );

            await FailUploadsAndNotifyAsync(
                uploads: uploads,
                kind: NotificationKind.ReleaseFolderMissing,
                notificationMessage: $"Release folder path {releaseFolderPath} does not exist.",
                cancellationToken: cancellationToken
            );

            return;
        }

        if (!fileSystemService.DirectoryExists(archiveConfig.ArchiveFilesBasePath))
        {
            logger.LogError(
                "Archive folder {ArchiveFilesBasePath} does not exist for ArchiveConfig {ArchiveConfigId}",
                archiveConfig.ArchiveFilesBasePath,
                archiveConfig.Id
            );

            await FailUploadsAndNotifyAsync(
                uploads: uploads,
                kind: NotificationKind.ArchiveCreationFailed,
                notificationMessage: $"Archive folder {archiveConfig.ArchiveFilesBasePath} does not exist. Bearcat does not create it, so check that it exists or is mounted.",
                cancellationToken: cancellationToken
            );

            return;
        }

        var archiver = archiverFactory.GetByName(archiveConfig.ArchiverName);
        var archiveDirectoryPath = fileSystemService.CreateTempDirectory(
            archiveConfig.ArchiveFilesBasePath
        );

        var packingSettings = await archivePackingSettingsService.GetPackingSettingsAsync(
            archiveConfig: archiveConfig,
            archiver: archiver,
            cancellationToken: cancellationToken
        );

        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = archiveDirectoryPath,
            ArchiveFiles = [],
            ArchiveState = ArchiveState.Creating,
            ArchiveFileSizeMb = packingSettings.ArchiveFileSizeMb,
            CreatedAt = timeProvider.GetLocalNow(),
            Uploads = uploads.ToList(),
            ErrorMessages = [],
        };

        repository.Add(archive);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);
        using var userCancellableTransfer = cancellationRegistry.RegisterUserCancellableTransfer(
            transferIdentifier,
            cancellationToken
        );

        try
        {
            await PackReleaseFolderAndHashArchiveFilesAsync(
                archive: archive,
                archiver: archiver,
                releaseFolderPath: releaseFolderPath,
                packingSettings: packingSettings,
                cancellationToken: userCancellableTransfer.CancellationToken
            );
        }
        catch (OperationCanceledException) when (userCancellableTransfer.WasCanceledByUser)
        {
            await DeleteArchiveAndCancelWaitingUploadsAsync(
                archive: archive,
                waitingUploads: uploads,
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task FailUploadsAndNotifyAsync(
        IReadOnlyList<Upload> uploads,
        NotificationKind kind,
        string notificationMessage,
        CancellationToken cancellationToken
    )
    {
        foreach (var upload in uploads)
        {
            upload.UploadState = UploadState.Failed;
            notificationService.Create(
                kind: kind,
                message: notificationMessage,
                entity: upload,
                selector: n => n.Upload
            );
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task PackReleaseFolderAndHashArchiveFilesAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchivePackingSettings packingSettings,
        CancellationToken cancellationToken
    )
    {
        var archiveResult = await releaseFolderPackingService.PackReleaseFolderIntoArchiveAsync(
            archive: archive,
            archiver: archiver,
            releaseFolderPath: releaseFolderPath,
            packingSettings: packingSettings,
            cancellationToken: cancellationToken
        );

        if (!archiveResult.IsSuccess)
        {
            await failedArchiveCreationService.MarkArchiveCreationFailedAndFailWaitingUploadsAsync(
                archive: archive,
                errorMessages: archiveResult.ErrorMessages ?? [],
                cancellationToken: cancellationToken
            );

            return;
        }

        archive.ArchiveFiles = archiveResult
            .CreatedFileNames.Select(f => new ArchiveFile { FullFileName = f })
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        await archiveFileHashService.HashArchiveFilesAsync(archive, archiver, cancellationToken);
        archive.MarkAsCreatedAndSetWaitingUploadsPending();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            archive.ArchiveConfig.Id,
            archive.ArchiveFiles.Count
        );
    }

    private async Task DeleteArchiveAndCancelWaitingUploadsAsync(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Canceled the creation of archive {ArchiveId} on user request, deleting the archive and its folder {ArchiveFolderPath}",
            archive.Id,
            archive.ArchiveFolderPath
        );

        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
        repository.Remove(archive);

        uploadCancellationService.CancelUploadsAndNotify(
            waitingUploads,
            "Upload canceled because the archive creation was canceled."
        );

        logger.LogInformation(
            "Canceled {UploadCount} uploads that were waiting for the canceled archive creation",
            waitingUploads.Count
        );

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }
}
