using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageUploads;

public class UploadFilesService(
    IUploadFilesRepository repository,
    IHosterFactory hosterFactory,
    IFileSystemService fileSystemService,
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

            var semaphore = new SemaphoreSlim(maximumParallelUploads);

            var filesToUpload = upload
                .Archive!
                .ArchiveFiles
                .Where(f => upload.UploadedFiles.All(uf => uf.ArchiveFileId != f.Id))
                .ToList();

            var uploadTasks = filesToUpload.Select(async file =>
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
