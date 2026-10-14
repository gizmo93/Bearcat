using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.UploadPreparation;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    TimeProvider timeProvider,
    ILogger<UploadFilesService> logger,
    MissingFileValidationService missingFileValidationService,
    UploadFinalizationService finalizationService,
    UploadCancellationService uploadCancellationService,
    FileUploadExecutionService fileUploadExecutionService,
    UploadConcurrencyService concurrencyService,
    UploadPreparationService uploadPreparationService,
    ITransferProgressTracker progressTracker
)
{
    public TimeSpan UploadQueuePollDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan NewPendingUploadsPollDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan FileUploadTimeout
    {
        get => fileUploadExecutionService.FileUploadTimeout;
        set => fileUploadExecutionService.FileUploadTimeout = value;
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ResetOrphanedUploadsToPendingAsync(cancellationToken);
            await CancelUploadsWithUnprocessedCancellationRequestAsync(cancellationToken);
            await UploadPendingUploadsAsync(cancellationToken);
        }
        finally
        {
            concurrencyService.Dispose();
        }
    }

    private async Task UploadPendingUploadsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing pending uploads");

        var pendingUploads = await GetPendingUploadsWithoutMissingFilesAsync(
            uploadIdsToExclude: new HashSet<int>(),
            cancellationToken: cancellationToken
        );

        if (pendingUploads.Count == 0)
        {
            logger.LogInformation("No pending uploads found, skipping processing");
            return;
        }

        var hostersByName = hosterFactory.GetHostersByName();
        var uploadsInProgress = new UploadsInProgress(progressTracker);

        await PrepareAndStartUploadsAsync(
            uploads: pendingUploads,
            hostersByName: hostersByName,
            uploadsInProgress: uploadsInProgress,
            cancellationToken: cancellationToken
        );

        var nextPendingUploadCheck = DateTimeOffset.UtcNow.Add(NewPendingUploadsPollDelay);

        while (uploadsInProgress.HasUploadsOrRunningFileUploads)
        {
            await ApplyCancellationRequestsAsync(uploadsInProgress, cancellationToken);

            await RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
                uploadsInProgress,
                cancellationToken
            );

            await ApplyAvailableFileUploadResultsAsync(uploadsInProgress, cancellationToken);

            await StartFileUploadsWithinConcurrencyLimitsAsync(
                uploadsInProgress,
                cancellationToken
            );

            if (!uploadsInProgress.HasUploadsOrRunningFileUploads)
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= nextPendingUploadCheck)
            {
                await AddNewPendingUploadsAsync(
                    hostersByName: hostersByName,
                    uploadsInProgress: uploadsInProgress,
                    cancellationToken: cancellationToken
                );

                nextPendingUploadCheck = DateTimeOffset.UtcNow.Add(NewPendingUploadsPollDelay);
            }

            await WaitBeforeNextQueuePollAsync(cancellationToken);
        }

        uploadsInProgress.CompletedFileUploads.Writer.Complete();

        await ApplyAvailableFileUploadResultsAsync(uploadsInProgress, cancellationToken);

        logger.LogInformation("Finished processing pending uploads");
    }

    private async Task AddNewPendingUploadsAsync(
        IReadOnlyDictionary<string, IHoster> hostersByName,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var newPendingUploads = await GetPendingUploadsWithoutMissingFilesAsync(
            uploadIdsToExclude: uploadsInProgress.PickedUpUploadIds,
            cancellationToken: cancellationToken
        );

        await PrepareAndStartUploadsAsync(
            uploads: newPendingUploads,
            hostersByName: hostersByName,
            uploadsInProgress: uploadsInProgress,
            cancellationToken: cancellationToken
        );

        if (newPendingUploads.Count > 0)
        {
            logger.LogInformation(
                "Added {NewUploadCount} new uploads to the upload queue",
                newPendingUploads.Count
            );
        }
    }

    private async Task PrepareAndStartUploadsAsync(
        IReadOnlyList<Upload> uploads,
        IReadOnlyDictionary<string, IHoster> hostersByName,
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        if (uploads.Count == 0)
        {
            return;
        }

        await concurrencyService.EnsureHosterSemaphoresAsync(
            uploads.Select(u => u.GetHosterClassName()).Distinct().ToList(),
            hostersByName: hostersByName,
            cancellationToken: cancellationToken
        );

        var hosterConfigsByRegistrationId = await repository.GetConfigByHosterRegistrationId(
            cancellationToken
        );

        foreach (var upload in uploads)
        {
            var context = await uploadPreparationService.PrepareUploadForUploadingAsync(
                upload: upload,
                hostersByName: hostersByName,
                hosterConfigsByRegistrationId: hosterConfigsByRegistrationId,
                uploadsInProgress: uploadsInProgress,
                cancellationToken: cancellationToken
            );

            if (context is null)
            {
                continue;
            }

            uploadsInProgress.PickedUpUploadIds.Add(upload.Id);

            if (context.TotalFileCount == 0)
            {
                context.Dispose();
                continue;
            }

            if (finalizationService.TryFinalizeUpload(context))
            {
                continue;
            }

            uploadsInProgress.StartUploading(context);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task StartFileUploadsWithinConcurrencyLimitsAsync(
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var context in GetUploadsWithPendingFilesRunningUploadsFirstThenOldest(
                uploadsInProgress
            )
        )
        {
            while (context.PendingFiles.Count > 0)
            {
                if (!await concurrencyService.TryAcquireGlobalSlotAsync(cancellationToken))
                {
                    return;
                }

                var fileToUpload = context.PendingFiles.Peek();

                var (hosterSlotAcquired, hosterSemaphore) =
                    await concurrencyService.TryAcquireHosterSlotAsync(
                        fileToUpload.HosterClassName,
                        cancellationToken
                    );

                if (!hosterSlotAcquired)
                {
                    concurrencyService.ReleaseGlobalSlot();
                    break;
                }

                context.PendingFiles.Dequeue();
                context.RunningFileCount++;

                uploadsInProgress.RunningFileUploadTasks.Add(
                    fileUploadExecutionService.UploadFileAsync(
                        fileToUpload: fileToUpload,
                        context: context,
                        concurrencyService: concurrencyService,
                        hosterSemaphore: hosterSemaphore!,
                        resultWriter: uploadsInProgress.CompletedFileUploads.Writer,
                        processCancellationToken: cancellationToken
                    )
                );
            }
        }
    }

    private static List<UploadExecutionContext> GetUploadsWithPendingFilesRunningUploadsFirstThenOldest(
        UploadsInProgress uploadsInProgress
    )
    {
        return uploadsInProgress
            .ContextsByUploadId.Values.Where(context =>
                !context.CancellationRequested && context.PendingFiles.Count > 0
            )
            .OrderByDescending(context => context.RunningFileCount > 0)
            .ThenBy(context => context.UploadId)
            .ToList();
    }

    private async Task WaitBeforeNextQueuePollAsync(CancellationToken cancellationToken)
    {
        if (UploadQueuePollDelay == TimeSpan.Zero)
        {
            await Task.Yield();
            return;
        }

        await Task.Delay(UploadQueuePollDelay, cancellationToken);
    }

    private async Task<List<Upload>> GetPendingUploadsWithoutMissingFilesAsync(
        IReadOnlySet<int> uploadIdsToExclude,
        CancellationToken cancellationToken
    )
    {
        var pendingUploads = await repository.GetPendingUploadsAsync(
            uploadIdsToExclude: uploadIdsToExclude,
            cancellationToken: cancellationToken
        );

        var uploadsToSkip = await missingFileValidationService.GetUploadsWithMissingFilesAsync(
            pendingUploads,
            cancellationToken
        );

        return pendingUploads.Except(uploadsToSkip).ToList();
    }

    private async Task ApplyAvailableFileUploadResultsAsync(
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

    private async Task ApplyFileUploadResultAsync(
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

    private async Task RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var completedTasks = uploadsInProgress
            .RunningFileUploadTasks.Where(t => t.IsCompleted)
            .ToList();

        foreach (var task in completedTasks)
        {
            uploadsInProgress.RunningFileUploadTasks.Remove(task);

            try
            {
                await task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in file upload worker");
            }
        }
    }

    private async Task ApplyCancellationRequestsAsync(
        UploadsInProgress uploadsInProgress,
        CancellationToken cancellationToken
    )
    {
        var cancellationRequestedUploadIds =
            await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        var hasChanges = false;

        foreach (var uploadId in cancellationRequestedUploadIds)
        {
            if (uploadsInProgress.ContextsByUploadId.TryGetValue(uploadId, out var context))
            {
                if (!context.CancellationRequested)
                {
                    context.RequestCancellation();
                    hasChanges = true;
                }

                if (finalizationService.TryFinalizeUpload(context))
                {
                    uploadsInProgress.RemoveFinishedUpload(uploadId);
                    hasChanges = true;
                }

                continue;
            }

            if (await CancelUploadWithoutRunningContextAsync(uploadId, cancellationToken))
            {
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ResetOrphanedUploadsToPendingAsync(CancellationToken cancellationToken)
    {
        var orphanedUploads = await repository.GetOrphanedUploadsAsync(cancellationToken);

        foreach (var upload in orphanedUploads)
        {
            logger.LogInformation("Cleaning up orphaned upload {UploadId}", upload.Id);
            upload.UploadState = UploadState.Pending;
        }

        await repository.SaveChangesAsync(cancellationToken);
        repository.ClearChangeTracker();
    }

    private async Task CancelUploadsWithUnprocessedCancellationRequestAsync(
        CancellationToken cancellationToken
    )
    {
        var uploadIds = await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        foreach (var uploadId in uploadIds)
        {
            await CancelUploadWithoutRunningContextAsync(uploadId, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        repository.ClearChangeTracker();
    }

    private async Task<bool> CancelUploadWithoutRunningContextAsync(
        int uploadId,
        CancellationToken cancellationToken
    )
    {
        var upload = await repository.GetUploadByIdAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return false;
        }

        uploadCancellationService.CancelUploadAndNotify(upload, "Upload canceled");

        return true;
    }
}
