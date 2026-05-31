using System.Threading.Channels;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    ILogger<UploadFilesService> logger,
    INotificationService notificationService,
    HosterCaptchaVerificationService captchaVerificationService
)
{
    private const int MaxParallelUploads = 10;

    private readonly SemaphoreSlim globalUploadSemaphore = new(
        initialCount: MaxParallelUploads,
        maxCount: MaxParallelUploads
    );

    private Dictionary<string, SemaphoreSlim> hosterUploadSemaphores = new();

    public TimeSpan UploadQueuePollDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan NewPendingUploadsPollDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan FileUploadTimeout { get; set; } = Timeout.InfiniteTimeSpan;

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CleanupOrphanedUploadsAsync(cancellationToken);
            await FinalizeUnprocessedCancellationRequestsAsync(cancellationToken);
            await ProcessPendingUploadsAsync(cancellationToken);
        }
        finally
        {
            DisposeSemaphores();
        }
    }

    private async Task ProcessPendingUploadsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing pending uploads");

        var pendingUploads = await GetPendingUploadsAsync(cancellationToken: cancellationToken);

        if (pendingUploads.Count == 0)
        {
            logger.LogInformation("No pending uploads found, skipping processing");
            return;
        }

        var hosters = hosterFactory.GetHostersByName().ToDictionary();

        await SetMaxParallelUploadsPerHosterSemaphoresAsync(
            hostersByName: hosters,
            cancellationToken: cancellationToken
        );

        var uploadContexts = new Dictionary<int, UploadExecutionContext>();
        var trackedUploadIds = new HashSet<int>();
        var runningUploadTasks = new List<Task>();

        var channel = Channel.CreateUnbounded<FileUploadCompleted>(
            options: new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );

        await AddUploadContextsAsync(
            uploads: pendingUploads,
            hosters: hosters,
            uploadContexts: uploadContexts,
            trackedUploadIds: trackedUploadIds,
            cancellationToken: cancellationToken
        );

        var nextPendingUploadCheck = DateTimeOffset.UtcNow.Add(NewPendingUploadsPollDelay);

        while (uploadContexts.Count > 0 || runningUploadTasks.Count > 0)
        {
            await HandleCancellationRequestsAsync(
                uploadContexts: uploadContexts,
                cancellationToken: cancellationToken
            );

            await HandleCompletedUploadTasksAsync(
                runningUploadTasks: runningUploadTasks,
                cancellationToken: cancellationToken
            );

            await HandleAvailableFileUploadResultsAsync(
                reader: channel.Reader,
                uploadContexts: uploadContexts,
                cancellationToken: cancellationToken
            );

            await ScheduleAvailableFileUploadsAsync(
                uploadContexts: uploadContexts,
                runningUploadTasks: runningUploadTasks,
                resultWriter: channel.Writer,
                cancellationToken: cancellationToken
            );

            if (uploadContexts.Count == 0 && runningUploadTasks.Count == 0)
            {
                break;
            }

            if (DateTimeOffset.UtcNow >= nextPendingUploadCheck)
            {
                var newPendingUploads = await GetPendingUploadsAsync(
                    trackedUploadIds,
                    cancellationToken
                );

                await AddUploadContextsAsync(
                    uploads: newPendingUploads,
                    hosters: hosters,
                    uploadContexts: uploadContexts,
                    trackedUploadIds: trackedUploadIds,
                    cancellationToken: cancellationToken
                );

                if (newPendingUploads.Count > 0)
                {
                    logger.LogInformation(
                        "Added {NewUploadCount} new uploads to the upload queue",
                        newPendingUploads.Count
                    );
                }

                nextPendingUploadCheck = DateTimeOffset.UtcNow.Add(NewPendingUploadsPollDelay);
            }

            await DelayQueuePollAsync(cancellationToken);
        }

        channel.Writer.Complete();

        await HandleAvailableFileUploadResultsAsync(
            reader: channel.Reader,
            uploadContexts: uploadContexts,
            cancellationToken: cancellationToken
        );

        logger.LogInformation("Finished processing pending uploads");
    }

    private async Task AddUploadContextsAsync(
        IReadOnlyList<Upload> uploads,
        Dictionary<string, IHoster> hosters,
        Dictionary<int, UploadExecutionContext> uploadContexts,
        HashSet<int> trackedUploadIds,
        CancellationToken cancellationToken
    )
    {
        if (uploads.Count == 0)
        {
            return;
        }

        var hosterConfigsByRegistrationId = await repository.GetConfigByHosterRegistrationId(
            cancellationToken
        );

        foreach (var upload in uploads)
        {
            trackedUploadIds.Add(upload.Id);

            var hosterClassName = upload.UploadConfig.HosterRegistration.HosterClassName;
            var hoster = hosters[hosterClassName];
            var hosterConfig = hoster.DeserializeHosterConfig(
                hosterConfigsByRegistrationId[upload.UploadConfig.HosterRegistrationId]
            );

            var context = await CreateUploadContextAsync(
                upload: upload,
                hoster: hoster,
                hosterConfig: hosterConfig,
                hosterClassName: hosterClassName,
                cancellationToken: cancellationToken
            );

            if (context.TotalFileCount == 0)
            {
                context.Dispose();
                continue;
            }

            if (TryFinalizeUpload(context))
            {
                continue;
            }

            upload.UploadState = UploadState.Uploading;
            uploadContexts[upload.Id] = context;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static async Task<UploadExecutionContext> CreateUploadContextAsync(
        Upload upload,
        IHoster hoster,
        IHosterConfig hosterConfig,
        string hosterClassName,
        CancellationToken cancellationToken
    )
    {
        var processedArchiveFileIds = upload
            .UploadedFiles.Select(uf => uf.ArchiveFileId)
            .ToHashSet();

        var successfulFileCount = upload.UploadedFiles.Count(uf => uf.ErrorMessages.Count == 0);
        var failedFileCount = upload.UploadedFiles.Count(uf => uf.ErrorMessages.Count > 0);

        var archiveFilesToUpload = upload
            .Archive!.ArchiveFiles.Where(af => !processedArchiveFileIds.Contains(af.Id))
            .ToList();

        var folderId = await CreateUploadFolderIdAsync(
            upload: upload,
            hoster: hoster,
            hosterConfig: hosterConfig,
            hasFilesToUpload: archiveFilesToUpload.Count > 0,
            cancellationToken: cancellationToken
        );

        var context = new UploadExecutionContext(
            upload: upload,
            totalFileCount: upload.Archive.ArchiveFiles.Count,
            successfulFileCount: successfulFileCount,
            failedFileCount: failedFileCount,
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
                    FolderId: folderId,
                    HosterClassName: hosterClassName,
                    Hoster: hoster,
                    HosterConfig: hosterConfig
                )
            );
        }

        return context;
    }

    private static async Task<string?> CreateUploadFolderIdAsync(
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

        return await folderHoster.CreateFolderAsync(folderName, hosterConfig, cancellationToken);
    }

    private async Task ScheduleAvailableFileUploadsAsync(
        Dictionary<int, UploadExecutionContext> uploadContexts,
        List<Task> runningUploadTasks,
        ChannelWriter<FileUploadCompleted> resultWriter,
        CancellationToken cancellationToken
    )
    {
        var scheduledAny = true;

        while (scheduledAny)
        {
            scheduledAny = false;

            foreach (var context in uploadContexts.Values.ToList())
            {
                if (context.CancellationRequested || context.PendingFiles.Count == 0)
                {
                    continue;
                }

                var fileToUpload = context.PendingFiles.Peek();

                if (!await globalUploadSemaphore.WaitAsync(0, cancellationToken))
                {
                    return;
                }

                var hosterSemaphore = hosterUploadSemaphores[fileToUpload.HosterClassName];

                if (!await hosterSemaphore.WaitAsync(0, cancellationToken))
                {
                    globalUploadSemaphore.Release();
                    continue;
                }

                context.PendingFiles.Dequeue();
                context.RunningFileCount++;
                scheduledAny = true;

                runningUploadTasks.Add(
                    Task.Run(
                        () =>
                            UploadFileAsync(
                                fileToUpload: fileToUpload,
                                context: context,
                                hosterSemaphore: hosterSemaphore,
                                resultWriter: resultWriter,
                                processCancellationToken: cancellationToken
                            ),
                        cancellationToken
                    )
                );
            }
        }
    }

    private async Task DelayQueuePollAsync(CancellationToken cancellationToken)
    {
        if (UploadQueuePollDelay == TimeSpan.Zero)
        {
            await Task.Yield();
            return;
        }

        await Task.Delay(UploadQueuePollDelay, cancellationToken);
    }

    private async Task<List<Upload>> GetPendingUploadsAsync(
        HashSet<int>? excludeUploadIds = null,
        CancellationToken cancellationToken = default
    )
    {
        var pendingUploads = await repository.GetPendingUploadsAsync(
            uploadIdsToExclude: excludeUploadIds ?? [],
            cancellationToken: cancellationToken
        );

        var uploadsToSkip = await HandleUploadsWithMissingFilesAsync(
            pendingUploads,
            cancellationToken
        );

        return pendingUploads.Except(uploadsToSkip).ToList();
    }

    private async Task UploadFileAsync(
        FileToUpload fileToUpload,
        UploadExecutionContext context,
        SemaphoreSlim hosterSemaphore,
        ChannelWriter<FileUploadCompleted> resultWriter,
        CancellationToken processCancellationToken
    )
    {
        using var fileUploadCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        if (FileUploadTimeout != Timeout.InfiniteTimeSpan)
        {
            fileUploadCancellationTokenSource.CancelAfter(FileUploadTimeout);
        }

        try
        {
            var fileDto = new FileDto(
                Id: fileToUpload.ArchiveFileId,
                FullFileName: fileToUpload.FullFileName,
                UploadId: fileToUpload.UploadId,
                FolderId: fileToUpload.FolderId
            );

            var result = await fileToUpload.Hoster.UploadFileAsync(
                fileDto,
                fileToUpload.HosterConfig,
                fileUploadCancellationTokenSource.Token
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: result.FileUrl,
                    ExternalId: result.ExternalId,
                    IsSuccess: result.IsSuccess,
                    Errors: result.ErrorMessages
                ),
                processCancellationToken
            );
        }
        catch (OperationCanceledException) when (processCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
            when (fileUploadCancellationTokenSource.IsCancellationRequested
                && !context.CancellationToken.IsCancellationRequested
                && !processCancellationToken.IsCancellationRequested
            )
        {
            var message = $"Upload timed out after {FormatTimeout(FileUploadTimeout)}";

            logger.LogWarning(
                "Upload for file {FilePath} for upload {UploadId} timed out after {Timeout}",
                fileToUpload.FullFileName,
                fileToUpload.UploadId,
                FormatTimeout(FileUploadTimeout)
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [message]
                ),
                processCancellationToken
            );
        }
        catch (CaptchaVerificationRequiredException ex)
        {
            captchaVerificationService.MarkRequired(context.Upload, ex.Message);
            context.RequestCancellation();

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [ex.Message],
                    WasCanceled: true
                ),
                processCancellationToken
            );
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [],
                    WasCanceled: true
                ),
                processCancellationToken
            );
        }
        catch (Exception ex)
        {
            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: new List<string> { ex.Message }
                ),
                processCancellationToken
            );
        }
        finally
        {
            globalUploadSemaphore.Release();
            hosterSemaphore.Release();
        }
    }

    private static string FormatTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return "infinite";
        }

        if (timeout.TotalSeconds < 1)
        {
            return $"{timeout.TotalMilliseconds:0}ms";
        }

        if (timeout.TotalMinutes < 1)
        {
            return $"{timeout.TotalSeconds:0.#}s";
        }

        if (timeout.TotalHours < 1)
        {
            return $"{timeout.TotalMinutes:0.#}m";
        }

        return $"{timeout.TotalHours:0.#}h";
    }

    private async Task HandleAvailableFileUploadResultsAsync(
        ChannelReader<FileUploadCompleted> reader,
        Dictionary<int, UploadExecutionContext> uploadContexts,
        CancellationToken cancellationToken
    )
    {
        while (reader.TryRead(out var result))
        {
            await HandleFileUploadResultAsync(
                result: result,
                uploadContexts: uploadContexts,
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task HandleFileUploadResultAsync(
        FileUploadCompleted result,
        Dictionary<int, UploadExecutionContext> uploadContexts,
        CancellationToken cancellationToken
    )
    {
        if (!uploadContexts.TryGetValue(result.UploadId, out var context))
        {
            return;
        }

        context.RunningFileCount--;

        result = await ApplyCancellationRequestToResultAsync(
            result: result,
            context: context,
            cancellationToken: cancellationToken
        );

        LogFileUploadResult(result);

        if (!result.WasCanceled)
        {
            ApplyFileUploadResult(context, result);
        }

        FinalizeUploadIfReady(context, uploadContexts);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<FileUploadCompleted> ApplyCancellationRequestToResultAsync(
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

    private void ApplyFileUploadResult(UploadExecutionContext context, FileUploadCompleted result)
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

    private void FinalizeUploadIfReady(
        UploadExecutionContext context,
        Dictionary<int, UploadExecutionContext> uploadContexts
    )
    {
        if (TryFinalizeUpload(context))
        {
            uploadContexts.Remove(context.UploadId);
        }
    }

    private bool TryFinalizeUpload(UploadExecutionContext context)
    {
        if (context.SuccessfulFileCount == context.TotalFileCount)
        {
            CompleteUpload(context);
            return true;
        }

        if (context is { CancellationRequested: true, HasOpenWork: false })
        {
            CancelUpload(context);
            return true;
        }

        if (context.ProcessedFileCount != context.TotalFileCount)
        {
            return false;
        }

        FailUpload(context);
        return true;
    }

    private void CompleteUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Completed;
        context.Upload.OnlineState = OnlineState.Online;
        context.Upload.UploadedAt = timeProvider.GetLocalNow();

        notificationService.CreateInfo(
            message: "All files uploaded successfully",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName,
            context.Upload.UploadState
        );

        context.Dispose();
    }

    private void FailUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Failed;
        context.Upload.OnlineState = OnlineState.PartiallyOnline;

        notificationService.CreateError(
            message: "Some files failed to upload",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName,
            context.Upload.UploadState
        );

        context.Dispose();
    }

    private void CancelUpload(UploadExecutionContext context)
    {
        context.Upload.UploadState = UploadState.Canceled;
        context.Upload.OnlineState = OnlineState.Unknown;

        notificationService.CreateInfo(
            message: "Upload canceled",
            entity: context.Upload,
            selector: n => n.Upload
        );

        logger.LogInformation(
            "Canceled upload for Upload {UploadId} to hoster {Hoster}",
            context.UploadId,
            context.Upload.UploadConfig.HosterRegistration.HosterClassName
        );

        context.Dispose();
    }

    private async Task HandleCompletedUploadTasksAsync(
        List<Task> runningUploadTasks,
        CancellationToken cancellationToken
    )
    {
        var completedTasks = runningUploadTasks.Where(t => t.IsCompleted).ToList();

        foreach (var task in completedTasks)
        {
            runningUploadTasks.Remove(task);

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

    private async Task HandleCancellationRequestsAsync(
        Dictionary<int, UploadExecutionContext> uploadContexts,
        CancellationToken cancellationToken
    )
    {
        var cancellationRequestedUploadIds =
            await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        var hasChanges = false;

        foreach (var uploadId in cancellationRequestedUploadIds)
        {
            if (uploadContexts.TryGetValue(uploadId, out var context))
            {
                if (!context.CancellationRequested)
                {
                    context.RequestCancellation();
                    hasChanges = true;
                }

                if (TryFinalizeUpload(context))
                {
                    uploadContexts.Remove(uploadId);
                    hasChanges = true;
                }

                continue;
            }

            var upload = await repository.GetUploadByIdAsync(uploadId, cancellationToken);

            if (upload is null)
            {
                continue;
            }

            upload.UploadState = UploadState.Canceled;
            upload.OnlineState = OnlineState.Unknown;

            notificationService.CreateInfo(
                message: "Upload canceled",
                entity: upload,
                selector: n => n.Upload
            );

            hasChanges = true;
        }

        if (hasChanges)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CleanupOrphanedUploadsAsync(CancellationToken cancellationToken)
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

    private async Task FinalizeUnprocessedCancellationRequestsAsync(
        CancellationToken cancellationToken
    )
    {
        var uploadIds = await repository.GetCancellationRequestedUploadIdsAsync(cancellationToken);

        foreach (var uploadId in uploadIds)
        {
            var upload = await repository.GetUploadByIdAsync(uploadId, cancellationToken);

            if (upload is null)
            {
                continue;
            }

            upload.UploadState = UploadState.Canceled;
            upload.OnlineState = OnlineState.Unknown;

            notificationService.CreateInfo(
                message: "Upload canceled",
                entity: upload,
                selector: n => n.Upload
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
        repository.ClearChangeTracker();
    }

    private async Task SetMaxParallelUploadsPerHosterSemaphoresAsync(
        Dictionary<string, IHoster> hostersByName,
        CancellationToken cancellationToken
    )
    {
        var hosterConfigs = await repository.GetConfigByHosterClassName(cancellationToken);
        var result = new Dictionary<string, SemaphoreSlim>();

        foreach (var (hosterName, hoster) in hostersByName)
        {
            // Skip hosters that are not in use
            if (!hosterConfigs.TryGetValue(hosterName, out var serializedConfig))
            {
                continue;
            }

            var hosterConfig = hoster.DeserializeHosterConfig(serializedConfig);

            var maxParallelUploads =
                await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;

            result[hosterName] = new SemaphoreSlim(maxParallelUploads);
        }

        hosterUploadSemaphores = result;
    }

    private async Task<List<Upload>> HandleUploadsWithMissingFilesAsync(
        IReadOnlyList<Upload> pendingUploads,
        CancellationToken cancellationToken
    )
    {
        var uploadsToSkip = new List<Upload>();

        foreach (var upload in pendingUploads)
        {
            var hasNonExistingFiles = await HandleNonExistingArchiveFilesAsync(
                upload,
                cancellationToken
            );

            if (hasNonExistingFiles)
            {
                uploadsToSkip.Add(upload);
            }
        }

        return uploadsToSkip;
    }

    private async Task<bool> HandleNonExistingArchiveFilesAsync(
        Upload upload,
        CancellationToken cancellationToken
    )
    {
        if (upload.Archive is null)
        {
            return false;
        }

        var nonExistingFiles = upload
            .Archive!.ArchiveFiles.Where(af =>
                !fileSystemService.FileExists(af.FullFileName)
                || af.Archive.ArchiveState != ArchiveState.Created
            )
            .ToList();

        if (nonExistingFiles.Count == 0)
        {
            return false;
        }

        await HandleMissingFilesAsync(upload, nonExistingFiles, cancellationToken);

        return true;
    }

    private async Task HandleMissingFilesAsync(
        Upload upload,
        IReadOnlyList<ArchiveFile> nonExistingFiles,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "The following archive files for Upload {UploadId} do not exist: {FilePaths}",
            upload.Id,
            string.Join(", ", nonExistingFiles.Select(f => f.FullFileName))
        );

        notificationService.CreateWarning(
            message: GetMissingFilesNotificationMessage(upload.UploadConfig.Release.ReleaseType),
            entity: upload,
            selector: n => n.Upload
        );

        if (upload.Archive!.ArchiveState == ArchiveState.Created)
        {
            upload.Archive.ArchiveState = ArchiveState.MissingFiles;
        }

        upload.ArchiveId = null;
        upload.UploadState = UploadState.WaitingForArchive;

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string GetMissingFilesNotificationMessage(ReleaseType releaseType)
    {
        return releaseType switch
        {
            ReleaseType.Managed =>
                "The archive assigned upload has missing files, triggering re-packaging",
            ReleaseType.Unmanaged =>
                "The archive assigned upload has missing files. Refresh the unmanaged archive after providing the archive files.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(releaseType),
                $"Unknown release type, {releaseType}"
            ),
        };
    }

    private void DisposeSemaphores()
    {
        globalUploadSemaphore.Dispose();

        foreach (var semaphore in hosterUploadSemaphores.Values)
        {
            semaphore.Dispose();
        }
    }
}
