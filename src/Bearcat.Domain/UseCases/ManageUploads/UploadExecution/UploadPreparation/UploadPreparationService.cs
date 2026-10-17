using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution.UploadPreparation;

public class UploadPreparationService(
    HosterAccountPoolAccountSelectionService hosterAccountPoolAccountSelectionService,
    UploadConcurrencyService concurrencyService,
    HosterCredentialsRejectionService credentialsRejectionService,
    UploadHosterFolderService hosterFolderService,
    IReleaseReadRepository releaseReadRepository,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
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
        if (
            upload.UploadConfig.UploadSource == UploadSource.ArchiveFiles
            && upload.ArchiveId is null
        )
        {
            logger.LogInformation(
                "Pending upload {UploadId} has no archive assigned, moving it back to waiting for archive",
                upload.Id
            );

            upload.UploadState = UploadState.WaitingForArchive;
            return null;
        }

        var sourceFiles = await GetSourceFilesOrFailUploadAsync(upload, cancellationToken);

        if (sourceFiles is null)
        {
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
                sourceFiles: sourceFiles,
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
                sourceFiles: sourceFiles,
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

    private async Task<List<UploadSourceFile>?> GetSourceFilesOrFailUploadAsync(
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        if (upload.UploadConfig.UploadSource == UploadSource.ArchiveFiles)
        {
            return upload
                .Archive!.ArchiveFiles.Select(archiveFile => new UploadSourceFile(
                    TransferFileId: archiveFile.Id,
                    ArchiveFileId: archiveFile.Id,
                    VideoFileRelativePath: null,
                    FullFileName: archiveFile.FullFileName,
                    Md5Hash: archiveFile.Md5Hash
                ))
                .ToList();
        }

        var mainVideoSourceFile = await GetMainVideoSourceFileOrFailUploadAsync(
            upload,
            cancellationToken
        );

        return mainVideoSourceFile is null ? null : [mainVideoSourceFile];
    }

    private async Task<UploadSourceFile?> GetMainVideoSourceFileOrFailUploadAsync(
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        var release = upload.UploadConfig.Release;
        var mediaFiles = await releaseReadRepository.GetMediaFilesAsync(
            release.Id,
            cancellationToken
        );
        var mainVideo = ReleaseMediaFileSelector.SelectMainVideo(mediaFiles);

        if (mainVideo is null)
        {
            FailUpload(
                upload,
                "The release has no main video file because media metadata was not extracted or no media file contains a video stream."
            );
            return null;
        }

        var fullFileName = release.ReleaseFolderPath is null
            ? null
            : Path.Combine(release.ReleaseFolderPath, mainVideo.RelativePath);

        if (fullFileName is null || !fileSystemService.FileExists(fullFileName))
        {
            FailUpload(
                upload,
                $"The main video file {mainVideo.RelativePath} does not exist in the release folder."
            );
            return null;
        }

        return new UploadSourceFile(
            TransferFileId: mainVideo.MediaFileId,
            ArchiveFileId: null,
            VideoFileRelativePath: mainVideo.RelativePath,
            FullFileName: fullFileName,
            Md5Hash: null
        );
    }

    private void FailUpload(Upload upload, string errorMessage)
    {
        logger.LogWarning(
            "Failed to prepare video upload {UploadId}: {ErrorMessage}",
            upload.Id,
            errorMessage
        );

        upload.UploadState = UploadState.Failed;
        upload.ErrorMessages.Add(errorMessage);

        notificationService.Create(
            kind: NotificationKind.UploadFailed,
            message: errorMessage,
            entity: upload,
            selector: n => n.Upload
        );
    }

    private static List<UploadSourceFile> GetSourceFilesWithoutUploadedFile(
        Upload upload,
        IReadOnlyList<UploadSourceFile> sourceFiles
    )
    {
        return sourceFiles
            .Where(sourceFile => !upload.UploadedFiles.Any(sourceFile.IsSourceOf))
            .ToList();
    }

    private async Task<bool> TryAssignAccountOfHosterAccountPoolAsync(
        Upload upload,
        IReadOnlyList<UploadSourceFile> sourceFiles,
        IReadOnlyDictionary<int, string> hosterConfigsByRegistrationId,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var hosterRegistration =
            await hosterAccountPoolAccountSelectionService.SelectHosterRegistrationAsync(
                upload: upload,
                requiredBytes: GetSourceFilesWithoutUploadedFile(upload, sourceFiles)
                    .Sum(sourceFile =>
                        FileSizes.GetFileSizeBytesOrZeroWhenMissing(sourceFile.FullFileName)
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
        IReadOnlyList<UploadSourceFile> sourceFiles,
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

        var successfullyUploadedFiles = upload
            .UploadedFiles.Where(uf => uf.ErrorMessages.Count == 0)
            .ToList();

        var sourceFilesToUpload = GetSourceFilesWithoutUploadedFile(upload, sourceFiles);

        var plannedFiles = sourceFiles
            .Select(sourceFile => new TransferFile(
                FileId: sourceFile.TransferFileId,
                FileName: Path.GetFileName(sourceFile.FullFileName),
                SourceName: hosterRegistration.Name,
                SizeBytes: FileSizes.GetFileSizeBytesOrZeroWhenMissing(sourceFile.FullFileName),
                IsAlreadyTransferred: successfullyUploadedFiles.Any(sourceFile.IsSourceOf)
            ))
            .ToList();

        var folderId = await hosterFolderService.CreateUploadFolderWhenHosterSupportsFoldersAsync(
            upload: upload,
            hoster: hoster,
            hosterConfig: hosterConfig,
            hasFilesToUpload: sourceFilesToUpload.Count > 0,
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
            sourceFiles: sourceFiles,
            successfulFileCount: successfulFileCount,
            failedFileCount: failedFileCount,
            plannedFiles: plannedFiles,
            cancellationTokenSource: CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            )
        );

        foreach (var sourceFile in sourceFilesToUpload)
        {
            context.PendingFiles.Enqueue(
                new FileToUpload(
                    UploadId: upload.Id,
                    SourceFile: sourceFile,
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
