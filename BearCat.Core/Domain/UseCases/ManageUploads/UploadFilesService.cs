using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    ILogger<UploadFilesService> logger)
{
    public async Task ProcessPendingUploadsAsync(CancellationToken cancellationToken)
    {
        var pendingUploads = await repository.GetPendingUploadsAsync(cancellationToken);

        foreach (var uploads in pendingUploads.GroupBy(u => u.UploadConfig.HosterRegistration.HosterClassName))
        {
            var hoster = hosterFactory.GetByName(uploads.Key);
            var hosterConfig = hoster.DeserializeHosterConfig(
                uploads.First().UploadConfig.HosterRegistration.SerializedConfig);

            foreach (var upload in uploads)
            {
                logger.LogInformation("Starting upload for Upload {UploadId} to hoster {Hoster}",
                    upload.Id,
                    hoster.Name);

                await ProcessUploadAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    upload: upload,
                    cancellationToken: cancellationToken);
                
                logger.LogInformation("Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
                    upload.Id,
                    hoster.Name,
                    upload.UploadState);
            }
        }
    }

    private async Task ProcessUploadAsync(
        IHoster hoster,
        IHosterConfig hosterConfig,
        Upload upload,
        CancellationToken cancellationToken)
    {
        var maximumParallelUploads = await hoster.GetMaximumParallelUploadsAsync(hosterConfig, cancellationToken) ?? 1;

        var semaphore = new SemaphoreSlim(maximumParallelUploads);

        var uploadTasks = upload.Archive!.ArchiveFiles.Select(async file =>
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

        await Task.WhenAll(uploadTasks);

        var anyFailedUploads = uploadTasks.Any(t => t.IsFaulted || !t.Result.IsSuccess);

        upload.UploadedFiles = [];

        foreach (var task in uploadTasks)
        {
            if (task.IsFaulted)
            {
                logger.LogError(task.Exception, "Upload failed for file for upload {UploadId}", upload.Id);
                continue;
            }

            if (!task.Result.IsSuccess)
            {
                logger.LogError("Upload failed for file {FilePath} for upload {UploadId}: {ErrorMessages}",
                    task.Result.ArchiveFile.FullFileName,
                    upload.Id,
                    string.Join(", ", task.Result.ErrorMessages));

                upload.UploadedFiles.Add(new UploadedFile
                {
                    ArchiveFile = task.Result.ArchiveFile,
                    HosterFileLink = string.Empty,
                    OnlineState = OnlineState.Unknown,
                    CreatedAt = DateTime.UtcNow,
                    CheckedAt = DateTime.UtcNow
                });

                continue;
            }

            upload.UploadedFiles.Add(new UploadedFile
            {
                ArchiveFile = task.Result.ArchiveFile,
                HosterFileLink = task.Result.FileUrl!,
                OnlineState = OnlineState.Online,
                CreatedAt = DateTime.UtcNow,
                CheckedAt = DateTime.UtcNow
            });
        }

        upload.UploadState = anyFailedUploads ? UploadState.Failed : UploadState.Completed;
        upload.OnlineState = anyFailedUploads ? OnlineState.PartiallyOnline : OnlineState.Online;
        await repository.SaveChangesAsync(cancellationToken);
    }
}
