using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;
using Bearcat.Domain.UseCases.ManageUploads.UploadExecution;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution.UploadPreparation;

public class UploadPreparationService(
    HosterAccountPoolAccountSelectionService hosterAccountPoolAccountSelectionService,
    UploadConcurrencyService concurrencyService,
    HosterCredentialsRejectionService credentialsRejectionService,
    UploadHosterFolderService hosterFolderService,
    ILogger<UploadPreparationService> logger
)
{
    public async Task<UploadExecutionContext?> PrepareUploadForUploadingAsync(
        Upload upload,
        IReadOnlyDictionary<string, IHoster> hostersByName,
        IReadOnlyDictionary<int, string> hosterConfigsByRegistrationId,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        if (upload.ArchiveId is null)
        {
            logger.LogInformation(
                "Pending upload {UploadId} has no archive assigned, moving it back to waiting for archive",
                upload.Id
            );

            upload.UploadState = UploadState.WaitingForArchive;
            return null;
        }

        if (upload.ShouldSelectAnotherAccountOfHosterAccountPool())
        {
            logger.LogInformation(
                "Account {HosterRegistration} assigned to upload {UploadId} is no longer usable for uploads, selecting another account of hoster account pool {HosterAccountPool}",
                upload.HosterRegistration!.Name,
                upload.Id,
                upload.UploadConfig.HosterAccountPool!.Name
            );

            upload.RemoveHosterRegistrationAndFolderAssignment();
        }

        if (
            !upload.HasHosterRegistrationAssigned()
            && !await TryAssignAccountOfHosterAccountPoolAsync(
                upload: upload,
                hosterConfigsByRegistrationId: hosterConfigsByRegistrationId,
                uploadsInProgress: uploadsInProgress,
                cancellationToken: cancellationToken
            )
        )
        {
            return null;
        }

        var hosterRegistration = upload.HosterRegistration!;
        var hosterClassName = hosterRegistration.HosterClassName;

        if (!hosterRegistration.IsActive || !concurrencyService.HasHosterSemaphore(hosterClassName))
        {
            logger.LogInformation(
                "Skipping pending upload {UploadId} because its hoster registration {HosterRegistration} is inactive or its parallel upload limit could not be determined",
                upload.Id,
                hosterRegistration.Name
            );

            return null;
        }

        var hoster = hostersByName[hosterClassName];
        var hosterConfig = hoster.DeserializeHosterConfig(
            hosterConfigsByRegistrationId[hosterRegistration.Id]
        );

        try
        {
            return await CreateUploadExecutionContextAsync(
                upload: upload,
                hoster: hoster,
                hosterConfig: hosterConfig,
                hosterClassName: hosterClassName,
                cancellationToken: cancellationToken
            );
        }
        catch (HosterCredentialsRejectedException ex)
        {
            credentialsRejectionService.DeactivateAndNotify(upload, ex.Message);

            if (upload.IsHosterAccountPoolUploadWithoutUploadedFiles())
            {
                upload.RemoveHosterRegistrationAndFolderAssignment();
            }

            return null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Failed to prepare upload {UploadId}, leaving it pending and skipping it for now",
                upload.Id
            );

            return null;
        }
    }

    private async Task<bool> TryAssignAccountOfHosterAccountPoolAsync(
        Upload upload,
        IReadOnlyDictionary<int, string> hosterConfigsByRegistrationId,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistration =
            await hosterAccountPoolAccountSelectionService.SelectHosterRegistrationAsync(
                upload: upload,
                requiredBytes: upload
                    .GetArchiveFilesWithoutUploadedFile()
                    .Sum(archiveFile =>
                        FileSizes.GetFileSizeBytesOrZeroWhenMissing(archiveFile.FullFileName)
                    ),
                hosterConfigsByRegistrationId: hosterConfigsByRegistrationId,
                remainingBytesOfRunningUploadsByHosterRegistrationId: uploadsInProgress.GetRemainingBytesOfRunningUploadsByHosterRegistrationId(),
                cancellationToken: cancellationToken
            );

        if (hosterRegistration is null)
        {
            return false;
        }

        upload.HosterRegistrationId = hosterRegistration.Id;
        upload.HosterRegistration = hosterRegistration;

        return true;
    }

    private async Task<UploadExecutionContext> CreateUploadExecutionContextAsync(
        Upload upload,
        IHoster hoster,
        IHosterConfig hosterConfig,
        string hosterClassName,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistration = upload.HosterRegistration!;

        using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(hosterRegistration);

        var successfulFileCount = upload.UploadedFiles.Count(uf => uf.ErrorMessages.Count == 0);
        var failedFileCount = upload.UploadedFiles.Count(uf => uf.ErrorMessages.Count > 0);

        var successfullyUploadedArchiveFileIds = upload
            .UploadedFiles.Where(uf => uf.ErrorMessages.Count == 0)
            .Select(uf => uf.ArchiveFileId)
            .ToHashSet();

        var archiveFilesToUpload = upload.GetArchiveFilesWithoutUploadedFile();

        var plannedFiles = upload
            .Archive!.ArchiveFiles.Select(archiveFile => new TransferFile(
                FileId: archiveFile.Id,
                FileName: Path.GetFileName(archiveFile.FullFileName),
                SourceName: hosterRegistration.Name,
                SizeBytes: FileSizes.GetFileSizeBytesOrZeroWhenMissing(archiveFile.FullFileName),
                IsAlreadyTransferred: successfullyUploadedArchiveFileIds.Contains(archiveFile.Id)
            ))
            .ToList();

        var folderId = await hosterFolderService.CreateUploadFolderWhenHosterSupportsFoldersAsync(
            upload: upload,
            hoster: hoster,
            hosterConfig: hosterConfig,
            hasFilesToUpload: archiveFilesToUpload.Count > 0,
            cancellationToken: cancellationToken
        );

        if (folderId is not null)
        {
            upload.HosterFolderId = folderId;

            await hosterFolderService.MoveUploadedFilesIntoFolderAsync(
                upload: upload,
                hoster: hoster,
                hosterConfig: hosterConfig,
                folderId: folderId,
                cancellationToken: cancellationToken
            );
        }

        var context = new UploadExecutionContext(
            upload: upload,
            totalFileCount: upload.Archive.ArchiveFiles.Count,
            successfulFileCount: successfulFileCount,
            failedFileCount: failedFileCount,
            plannedFiles: plannedFiles,
            cancellationTokenSource: CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            )
        );

        foreach (var archiveFile in archiveFilesToUpload)
        {
            context.PendingFiles.Enqueue(
                new FileToUpload(
                    UploadId: upload.Id,
                    ArchiveFileId: archiveFile.Id,
                    FullFileName: archiveFile.FullFileName,
                    Md5Hash: archiveFile.Md5Hash,
                    FolderId: folderId,
                    HosterClassName: hosterClassName,
                    Hoster: hoster,
                    HosterConfig: hosterConfig
                )
            );
        }

        return context;
    }
}
