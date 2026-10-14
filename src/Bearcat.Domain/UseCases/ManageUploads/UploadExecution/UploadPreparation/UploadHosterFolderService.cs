using System.Diagnostics;
using Bearcat.Abstractions.Hoster;
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

        foreach (var file in filesToMove)
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
                    upload.Id
                );
            }
            catch (Exception ex) when (ex is not HosterCredentialsRejectedException)
            {
                logger.LogWarning(
                    ex,
                    "Failed to move file {FileLink} to folder {FolderId} for upload {UploadId}, leaving it in its current folder",
                    file.HosterFileLink,
                    folderId,
                    upload.Id
                );
            }
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
