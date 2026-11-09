using System.Diagnostics;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution.UploadPreparation;

public class UploadHosterFolderService(ILogger<UploadHosterFolderService> logger)
{
    public int FolderCreationRetryAttempts { get; set; } = 3;

    public TimeSpan FolderCreationRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<string?> CreateUploadFolderWhenHosterSupportsFoldersAsync(
        Upload upload,
        IHoster hoster,
        IHosterConfig hosterConfig,
        bool hasFilesToUpload,
        CancellationToken cancellationToken
    )
    {
        if (!hasFilesToUpload || hoster is not IHosterWithFolders folderHoster)
        {
            return null;
        }

        var folderName = $"{upload.UploadConfig.Release.Name}_UploadId_{upload.Id}";

        return await CreateFolderWithRetryAsync(
            folderHoster: folderHoster,
            folderName: folderName,
            hosterConfig: hosterConfig,
            uploadId: upload.Id,
            cancellationToken: cancellationToken
        );
    }

    public async Task MoveUploadedFilesIntoFolderAsync(
        Upload upload,
        IHoster hoster,
        IHosterConfig hosterConfig,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        if (hoster is not IHosterWithFolders folderHoster)
        {
            return;
        }

        var filesToMove = upload
            .UploadedFiles.Where(uf => uf.HasHosterFileLink() && uf.HosterFolderId != folderId)
            .ToList();

        if (filesToMove.Count == 0)
        {
            return;
        }

        if (folderHoster is IHosterWithBatchFileMove batchFileMoveHoster)
        {
            await MoveUploadedFilesIntoFolderInOneRequestAsync(
                filesToMove: filesToMove,
                batchFileMoveHoster: batchFileMoveHoster,
                hosterConfig: hosterConfig,
                folderId: folderId,
                uploadId: upload.Id,
                cancellationToken: cancellationToken
            );

            return;
        }

        foreach (var file in filesToMove)
        {
            await MoveUploadedFileIntoFolderAsync(
                file: file,
                folderHoster: folderHoster,
                hosterConfig: hosterConfig,
                folderId: folderId,
                uploadId: upload.Id,
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task MoveUploadedFilesIntoFolderInOneRequestAsync(
        List<UploadedFile> filesToMove,
        IHosterWithBatchFileMove batchFileMoveHoster,
        IHosterConfig hosterConfig,
        string folderId,
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var movedFileLinks = await batchFileMoveHoster.MoveFilesToFolderAsync(
                files: filesToMove
                    .Select(file => new HosterFileToMoveDto(file.HosterFileLink, file.ExternalId))
                    .ToList(),
                folderId: folderId,
                hosterConfig: hosterConfig,
                cancellationToken: cancellationToken
            );

            foreach (var file in filesToMove.Where(f => movedFileLinks.Contains(f.HosterFileLink)))
            {
                file.HosterFolderId = folderId;
            }

            logger.LogInformation(
                "Moved {MovedFileCount} of {FileCount} carried-over files to folder {FolderId} for upload {UploadId}",
                movedFileLinks.Count,
                filesToMove.Count,
                folderId,
                uploadId
            );
        }
        catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
        {
            logger.LogWarning(
                ex,
                "Failed to move {FileCount} carried-over files to folder {FolderId} for upload {UploadId}, leaving them in their current folders",
                filesToMove.Count,
                folderId,
                uploadId
            );
        }
    }

    private async Task MoveUploadedFileIntoFolderAsync(
        UploadedFile file,
        IHosterWithFolders folderHoster,
        IHosterConfig hosterConfig,
        string folderId,
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await folderHoster.MoveFileToFolderAsync(
                fileUrl: file.HosterFileLink,
                externalId: file.ExternalId,
                folderId: folderId,
                hosterConfig: hosterConfig,
                cancellationToken: cancellationToken
            );

            file.HosterFolderId = folderId;

            logger.LogInformation(
                "Moved carried-over file {FileLink} to folder {FolderId} for upload {UploadId}",
                file.HosterFileLink,
                folderId,
                uploadId
            );
        }
        catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
        {
            logger.LogWarning(
                ex,
                "Failed to move file {FileLink} to folder {FolderId} for upload {UploadId}, leaving it in its current folder",
                file.HosterFileLink,
                folderId,
                uploadId
            );
        }
    }

    private async Task<string> CreateFolderWithRetryAsync(
        IHosterWithFolders folderHoster,
        string folderName,
        IHosterConfig hosterConfig,
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        foreach (var attempt in Enumerable.Range(1, FolderCreationRetryAttempts))
        {
            try
            {
                return await folderHoster.CreateFolderAsync(
                    folderName,
                    hosterConfig,
                    cancellationToken
                );
            }
            catch (Exception ex)
                when (attempt < FolderCreationRetryAttempts
                    && ex is not HosterCredentialsRejectedException
                )
            {
                logger.LogWarning(
                    ex,
                    "Failed to create folder {FolderName} for upload {UploadId} on attempt {Attempt}/{MaxAttempts}, retrying in {RetryDelay}",
                    folderName,
                    uploadId,
                    attempt,
                    FolderCreationRetryAttempts,
                    FolderCreationRetryDelay
                );

                await Task.Delay(FolderCreationRetryDelay, cancellationToken);
            }
        }

        throw new UnreachableException();
    }
}
