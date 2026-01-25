using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    ILogger<UploadFilesService> logger)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await CleanupOrphanedUploadsAsync(cancellationToken);
        await ProcessPendingUploadsAsync(cancellationToken);
    }

    private async Task ProcessPendingUploadsAsync(CancellationToken cancellationToken)
    {
        var pendingUploads = await repository.GetPendingUploadsAsync(cancellationToken);

        foreach (var uploads in pendingUploads.GroupBy(u => u.UploadConfig.HosterRegistration.HosterClassName))
        {
            var hoster = hosterFactory.GetByName(uploads.Key);
            var hosterConfig = hoster.DeserializeHosterConfig(
                uploads.First().UploadConfig.HosterRegistration.SerializedConfig);

            foreach (var upload in uploads)
            {
                var hasNonExistingFiles = await HandleNonExistingArchiveFilesAsync(upload, cancellationToken);
                if (hasNonExistingFiles)
                {
                    logger.LogInformation(
                        "Skipping upload {UploadId} due to non-existing archive files, re-archiving was requested",
                        upload.Id);
                    continue;
                }

                logger.LogInformation("Starting upload for Upload {UploadId} to hoster {Hoster}",
                    upload.Id,
                    hoster.Name);

                await ProcessUploadAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    upload: upload,
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
                    upload.Id,
                    hoster.Name,
                    upload.UploadState);
            }
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

    private async Task ProcessUploadAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        Upload upload,
        CancellationToken cancellationToken)
    {
        try
        {
            var maximumParallelUploads =
                await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;

            using var semaphore = new SemaphoreSlim(maximumParallelUploads);

            var filesToUpload = upload
                .Archive!
                .ArchiveFiles
                .Where(f => upload.UploadedFiles.All(uf => uf.ArchiveFileId != f.Id))
                .ToList();

            upload.UploadState = UploadState.Uploading;
            await repository.SaveChangesAsync(cancellationToken);

            var uploadTasks = StartUploadTasks(
                upload: upload,
                filesToUpload: filesToUpload,
                semaphore: semaphore,
                hoster: hoster,
                hosterConfig: hosterConfig,
                cancellationToken: cancellationToken);

            var finishedTasks = new HashSet<Task<UploadFileResult>>();

            while (uploadTasks.Any(t => !t.IsCompleted))
            {
                try
                {
                    await PersistIntermediateResultsAsync(
                        upload: upload,
                        uploadTasks: uploadTasks,
                        finishedTasks: finishedTasks,
                        cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError("Exception during persisting intermediate upload results for Upload {UploadId}: {Message}",
                        upload.Id,
                        e.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }

            await PersistIntermediateResultsAsync(
                upload: upload,
                uploadTasks: uploadTasks,
                finishedTasks: finishedTasks,
                cancellationToken: cancellationToken);

            var anyFailedUploads = uploadTasks.Any(t => t.IsFaulted || !t.Result.IsSuccess);

            upload.UploadState = anyFailedUploads ? UploadState.Failed : UploadState.Completed;
            upload.OnlineState = anyFailedUploads ? OnlineState.PartiallyOnline : OnlineState.Online;
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            upload.UploadState = UploadState.Failed;
            upload.ErrorMessages.Add($"Exception during upload processing: {e.Message}");

            logger.LogError(e, "Exception during upload processing for Upload {UploadId}: {Message}",
                upload.Id,
                e.Message);

            await repository.SaveChangesAsync(cancellationToken);
        }
    }

    private List<Task<UploadFileResult>> StartUploadTasks(
        Upload upload,
        List<ArchiveFile> filesToUpload,
        SemaphoreSlim semaphore,
        IHoster hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        return filesToUpload.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await hoster.UploadFileAsync(file, hosterConfig, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError("Exception during upload of file {FilePath} for upload {UploadId}: {Exception}",
                        file.FullFileName,
                        upload.Id,
                        ex);
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToList();
    }

    private async Task PersistIntermediateResultsAsync(
        Upload upload,
        List<Task<UploadFileResult>> uploadTasks,
        HashSet<Task<UploadFileResult>> finishedTasks,
        CancellationToken cancellationToken)
    {
        var newlyFinishedTasks = uploadTasks
            .Where(t => !finishedTasks.Contains(t) && t.IsCompleted)
            .ToHashSet();

        var missingUploadedFiles = newlyFinishedTasks
            .Select(t => t.Result)
            .Where(r => upload.UploadedFiles.All(u => u.ArchiveFile != r.ArchiveFile))
            .Select(r => new UploadedFile
            {
                ArchiveFile = r.ArchiveFile,
                HosterFileLink = r.FileUrl ?? string.Empty,
                OnlineState = r.IsSuccess ? OnlineState.Online : OnlineState.Unknown,
                ErrorMessages = r.ErrorMessages.ToList(),
                CreatedAt = timeProvider.GetLocalNow(),
                CheckedAt = timeProvider.GetLocalNow(),
            })
            .ToList();

        finishedTasks.UnionWith(newlyFinishedTasks);

        upload.UploadedFiles.AddRange(missingUploadedFiles);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private void LogUploadErrors(Upload upload, List<Task<UploadFileResult>> failedTasks)
    {
        foreach (var task in failedTasks)
        {
            if (task.IsFaulted)
            {
                logger.LogError(task.Exception, "Upload failed for file for upload {UploadId}", upload.Id);
                continue;
            }

            if (task.Result.IsSuccess)
            {
                continue;
            }

            logger.LogError("Upload failed for file {FilePath} for upload {UploadId}: {ErrorMessages}",
                task.Result.ArchiveFile.FullFileName,
                upload.Id,
                string.Join(", ", task.Result.ErrorMessages));
        }
    }

    private async Task<bool> HandleNonExistingArchiveFilesAsync(Upload upload, CancellationToken cancellationToken)
    {
        var nonExistingFiles = upload
            .Archive!
            .ArchiveFiles
            .Where(af => !fileSystemService.FileExists(af.FullFileName)
                         || af.Archive.ArchiveState != ArchiveState.Created)
            .ToList();

        if (nonExistingFiles.Count == 0)
        {
            return false;
        }

        logger.LogInformation("The following archive files for Upload {UploadId} do not exist: {FilePaths}",
            upload.Id,
            string.Join(", ", nonExistingFiles.Select(f => f.FullFileName)));

        if (upload.Archive.ArchiveState == ArchiveState.Created)
        {
            upload.Archive.ArchiveState = ArchiveState.MissingFiles;
        }

        upload.ArchiveId = null;
        upload.UploadState = UploadState.WaitingForArchive;

        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
