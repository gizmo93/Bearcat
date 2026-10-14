using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.UploadExecution.UploadPreparation;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    ILogger<UploadFilesService> logger,
    MissingFileValidationService missingFileValidationService,
    UploadFinalizationService finalizationService,
    FileUploadResultService fileUploadResultService,
    UploadCancellationRequestService uploadCancellationRequestService,
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
            await uploadCancellationRequestService.CancelUploadsWithUnprocessedCancellationRequestAsync(
                cancellationToken
            );
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
        var uploadsInProgress = new UploadsInProgress(progressTracker, logger);

        await PrepareAndStartUploadsAsync(
            uploads: pendingUploads,
            hostersByName: hostersByName,
            uploadsInProgress: uploadsInProgress,
            cancellationToken: cancellationToken
        );

        var nextPendingUploadCheck = DateTimeOffset.UtcNow.Add(NewPendingUploadsPollDelay);

        while (uploadsInProgress.HasUploadsOrRunningFileUploads)
        {
            await uploadCancellationRequestService.ApplyCancellationRequestsToUploadsInProgressAsync(
                uploadsInProgress,
                cancellationToken
            );

            await uploadsInProgress.RemoveCompletedFileUploadTasksAndLogUnexpectedErrorsAsync(
                cancellationToken
            );

            await fileUploadResultService.ApplyAvailableFileUploadResultsAsync(
                uploadsInProgress,
                cancellationToken
            );

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

        await fileUploadResultService.ApplyAvailableFileUploadResultsAsync(
            uploadsInProgress,
            cancellationToken
        );

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
}
