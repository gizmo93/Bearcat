using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class FileUploadResultService(
    IUploadFilesRepository repository,
    TimeProvider timeProvider,
    UploadFinalizationService finalizationService,
    ILogger<FileUploadResultService> logger
)
{
    public async Task ApplyAvailableFileUploadResultsAsync(
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        while (uploadsInProgress.CompletedFileUploads.Reader.TryRead(out var result))
        {
            await ApplyFileUploadResultAsync(
                result: result,
                uploadsInProgress: uploadsInProgress,
                cancellationToken: cancellationToken
            );
        }
    }

    public async Task ApplyFileUploadResultAsync(
        FileUploadCompleted result,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        if (!uploadsInProgress.ContextsByUploadId.TryGetValue(result.UploadId, out var context))
        {
            return;
        }

        context.RunningFileCount--;

        result = await MarkResultAsCanceledWhenCancellationWasRequestedAsync(
            result: result,
            context: context,
            cancellationToken: cancellationToken
        );

        LogFileUploadResult(result);

        if (!result.WasCanceled)
        {
            AddUploadedFileAndCountResult(context, result);
        }

        if (finalizationService.TryFinalizeUpload(context))
        {
            uploadsInProgress.RemoveFinishedUpload(context.UploadId);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<FileUploadCompleted> MarkResultAsCanceledWhenCancellationWasRequestedAsync(
        FileUploadCompleted result,
        UploadExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        if (result.IsSuccess)
        {
            return result;
        }

        if (context.CancellationRequested)
        {
            return result with { WasCanceled = true };
        }

        var cancellationRequested = await repository.IsCancellationRequestedAsync(
            result.UploadId,
            cancellationToken
        );

        if (!cancellationRequested)
        {
            return result;
        }

        context.RequestCancellation();
        return result with { WasCanceled = true };
    }

    private void LogFileUploadResult(FileUploadCompleted result)
    {
        logger.LogInformation(
            "Upload for file {FilePath} for upload {UploadId} completed with IsSuccess {Success}",
            result.FullFileName,
            result.UploadId,
            result.IsSuccess
        );
    }

    private void AddUploadedFileAndCountResult(
        UploadExecutionContext context,
        FileUploadCompleted result
    )
    {
        var archiveFile = context.Upload.Archive!.ArchiveFiles.Single(f =>
            f.Id == result.ArchiveFileId
        );

        context.Upload.UploadedFiles.Add(
            new UploadedFile
            {
                Upload = context.Upload,
                UploadId = context.UploadId,
                ArchiveFile = archiveFile,
                ArchiveFileId = result.ArchiveFileId,
                HosterFileLink = result.FileUrl ?? string.Empty,
                ExternalId = result.ExternalId,
                HosterFolderId = context.Upload.HosterFolderId,
                Md5Hash = result.IsSuccess ? result.Md5Hash ?? archiveFile.Md5Hash : null,
                ErrorMessages = result.Errors.ToList(),
                OnlineState = result.IsSuccess ? OnlineState.Online : OnlineState.Unknown,
                CreatedAt = timeProvider.GetLocalNow(),
                CheckedAt = timeProvider.GetLocalNow(),
            }
        );

        if (result is { IsSuccess: true, Errors.Count: 0 })
        {
            context.SuccessfulFileCount++;
        }
        else
        {
            context.FailedFileCount++;
        }
    }
}
