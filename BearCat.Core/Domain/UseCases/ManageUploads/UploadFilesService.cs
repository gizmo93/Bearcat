using System.Collections.Concurrent;
using System.Threading.Channels;
using BearCat.Core.Domain.Abstractions;
using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageUploads.Dto;
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
    private const int MaxParallelUploads = 10;

    private readonly SemaphoreSlim saveChangesSemaphore = new(
        initialCount: 1,
        maxCount: 1);

    private readonly SemaphoreSlim globalUploadSemaphore = new(
        initialCount: MaxParallelUploads,
        maxCount: MaxParallelUploads);

    private Dictionary<string, SemaphoreSlim> hosterUploadSemaphores = new();

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await CleanupOrphanedUploadsAsync(cancellationToken);
        await ProcessPendingUploads2Async(cancellationToken);
        DisposeSemaphores();
    }

    private async Task ProcessPendingUploads2Async(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing pending uploads");

        var pendingUploads = await GetPendingUploadsAsync(cancellationToken: cancellationToken);

        if (pendingUploads.Count == 0)
        {
            logger.LogInformation("No pending uploads found, skipping processing");
            return;
        }

        var hosters = pendingUploads
            .GroupBy(u => u.UploadConfig.HosterRegistration.HosterClassName)
            .ToDictionary(
                g => g.Key,
                g => hosterFactory.GetByName(g.Key));

        var hosterConfigByHosterName = pendingUploads
            .GroupBy(u => u.UploadConfig.HosterRegistration.HosterClassName)
            .ToDictionary(
                g => g.Key,
                g => hosters[g.Key].DeserializeHosterConfig(
                    g.First().UploadConfig.HosterRegistration.SerializedConfig));

        var uploadQueue = new ConcurrentQueue<FileToUpload>(
            GetFilesToUpload(
                uploads: pendingUploads,
                hosters: hosters,
                hosterConfigByHosterName: hosterConfigByHosterName));

        var channel = Channel.CreateUnbounded<FileUploadCompleted>(
            options: new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        var uploadResultHandlerTask = Task.Run(() => HandleFileUploadResultsAsync(
                reader: channel.Reader,
                cancellationToken: cancellationToken),
            cancellationToken);

        var uploadFilesTask = Task.Run(() => UploadFilesAsync(
                uploadQueue: uploadQueue,
                resultWriter: channel.Writer,
                hostersByName: hosters,
                hosterConfigsByHoster: hosterConfigByHosterName,
                cancellationToken: cancellationToken),
            cancellationToken);

        var pendingUploadIds = pendingUploads
            .Select(u => u.Id)
            .ToHashSet();

        while (!uploadFilesTask.IsCompleted)
        {
            var newPendingUploads = (await GetPendingUploadsAsync(pendingUploadIds, cancellationToken))
                .Where(u => !pendingUploadIds.Contains(u.Id))
                .ToList();

            var filesToUpload = GetFilesToUpload(
                uploads: newPendingUploads,
                hosters: hosters,
                hosterConfigByHosterName: hosterConfigByHosterName);

            foreach (var file in filesToUpload)
            {
                uploadQueue.Enqueue(file);
                pendingUploadIds.UnionWith(newPendingUploads.Select(u => u.Id));
            }

            if (newPendingUploads.Count > 0)
            {
                logger.LogInformation("Added {NewUploadCount} new uploads to the upload queue",
                    newPendingUploads.Count);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }

        channel.Writer.Complete();

        await uploadResultHandlerTask;
        logger.LogInformation("Finished processing pending uploads");
    }

    private static List<FileToUpload> GetFilesToUpload(
        IReadOnlyList<Upload> uploads,
        Dictionary<string, IHoster> hosters,
        Dictionary<string, IHosterConfig> hosterConfigByHosterName)
    {
        return uploads
            .SelectMany(u => u.Archive!.ArchiveFiles
                .Where(af => u.UploadedFiles.All(uf => uf.ArchiveFileId != af.Id))
                .Select(f => new FileToUpload(
                    Upload: u,
                    ArchiveFile: f,
                    Hoster: hosters[u.UploadConfig.HosterRegistration.HosterClassName],
                    HosterConfig: hosterConfigByHosterName[u.UploadConfig.HosterRegistration.HosterClassName])))
            .ToList();
    }

    private async Task UploadFilesAsync(
        ConcurrentQueue<FileToUpload> uploadQueue,
        ChannelWriter<FileUploadCompleted> resultWriter,
        Dictionary<string, IHoster> hostersByName,
        Dictionary<string, IHosterConfig> hosterConfigsByHoster,
        CancellationToken cancellationToken)
    {
        await SetMaxParallelUploadsPerHosterSemaphoresAsync(
            hostersByName: hostersByName,
            hosterConfigsByHoster: hosterConfigsByHoster,
            cancellationToken: cancellationToken);

        var runningUploadTasks = new List<Task>();

        while (true)
        {
            while (uploadQueue.Count > 0)
            {
                if (uploadQueue.TryDequeue(out var file))
                {
                    runningUploadTasks.Add(
                        UploadFileAsync(
                            fileToUpload: file,
                            resultWriter: resultWriter,
                            cancellationToken: cancellationToken));
                }
            }

            runningUploadTasks = runningUploadTasks
                .Where(t => !t.IsCompleted)
                .ToList();

            if (uploadQueue.Count == 0 && runningUploadTasks.Count == 0)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task<List<Upload>> GetPendingUploadsAsync(
        HashSet<int>? excludeUploadIds = null,
        CancellationToken cancellationToken = default)
    {
        var pendingUploads = await repository.GetPendingUploadsAsync(
            uploadIdsToExclude: excludeUploadIds ?? [],
            cancellationToken: cancellationToken);

        var uploadsToSkip = await HandleUploadsWithMissingFilesAsync(pendingUploads, cancellationToken);

        return pendingUploads.Except(uploadsToSkip).ToList();
    }

    private async Task UploadFileAsync(
        FileToUpload fileToUpload,
        ChannelWriter<FileUploadCompleted> resultWriter,
        CancellationToken cancellationToken)
    {
        var hosterSemaphore =
            hosterUploadSemaphores[fileToUpload.Upload.UploadConfig.HosterRegistration.HosterClassName];
        await hosterSemaphore.WaitAsync(cancellationToken);
        await globalUploadSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (fileToUpload.Upload.UploadState != UploadState.Uploading)
            {
                fileToUpload.Upload.UploadState = UploadState.Uploading;
                await SaveChangesAsync(cancellationToken);
            }

            var result = await fileToUpload.Hoster.UploadFileAsync(
                fileToUpload.ArchiveFile,
                fileToUpload.HosterConfig,
                cancellationToken);

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    Upload: fileToUpload.Upload,
                    ArchiveFile: fileToUpload.ArchiveFile,
                    FileUrl: result.FileUrl,
                    IsSuccess: result.IsSuccess,
                    Errors: result.ErrorMessages),
                cancellationToken);
        }
        catch (Exception ex)
        {
            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    Upload: fileToUpload.Upload,
                    ArchiveFile: fileToUpload.ArchiveFile,
                    FileUrl: null,
                    IsSuccess: false,
                    Errors: new List<string> { ex.Message }),
                cancellationToken);
        }
        finally
        {
            globalUploadSemaphore.Release();
            hosterSemaphore.Release();
        }
    }

    private async Task HandleFileUploadResultsAsync(
        ChannelReader<FileUploadCompleted> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var result in reader.ReadAllAsync(cancellationToken))
        {
            logger.LogInformation(
                "Upload for file {FilePath} for upload {UploadId} completed with IsSuccess {Success}",
                result.ArchiveFile.FullFileName,
                result.Upload.Id,
                result.IsSuccess);

            result.Upload.UploadedFiles.Add(
                new UploadedFile
                {
                    ArchiveFile = result.ArchiveFile,
                    ArchiveFileId = result.ArchiveFile.Id,
                    HosterFileLink = result.FileUrl ?? string.Empty,
                    ErrorMessages = result.Errors.ToList(),
                    OnlineState = result.IsSuccess ? OnlineState.Online : OnlineState.Unknown,
                    CreatedAt = timeProvider.GetLocalNow(),
                    CheckedAt = timeProvider.GetLocalNow(),
                });

            var allFilesProcessed = result.Upload.Archive!
                .ArchiveFiles
                .All(f => result.Upload.UploadedFiles.Any(uf => uf.ArchiveFileId == f.Id));

            if (allFilesProcessed)
            {
                var anyFailedUploads = result.Upload.UploadedFiles.Any(uf => uf.ErrorMessages.Count > 0);

                result.Upload.UploadState = anyFailedUploads ? UploadState.Failed : UploadState.Completed;
                result.Upload.OnlineState = anyFailedUploads ? OnlineState.PartiallyOnline : OnlineState.Online;

                logger.LogInformation(
                    "Completed upload for Upload {UploadId} to hoster {Hoster} with state {UploadState}",
                    result.Upload.Id,
                    result.Upload.UploadConfig.HosterRegistration.HosterClassName,
                    result.Upload.UploadState);
            }

            await SaveChangesAsync(cancellationToken);
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

    private async Task SetMaxParallelUploadsPerHosterSemaphoresAsync(
        Dictionary<string, IHoster> hostersByName,
        Dictionary<string, IHosterConfig> hosterConfigsByHoster,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, SemaphoreSlim>();

        foreach (var (hosterName, hoster) in hostersByName)
        {
            var maxParallelUploads =
                await hoster.GetMaximumParallelUploadsAsync(
                    hosterConfigsByHoster[hosterName],
                    cancellationToken) ?? 1;

            result[hosterName] = new SemaphoreSlim(maxParallelUploads);
        }

        hosterUploadSemaphores = result;
    }

    private async Task<List<Upload>> HandleUploadsWithMissingFilesAsync(
        IReadOnlyList<Upload> pendingUploads,
        CancellationToken cancellationToken)
    {
        var uploadsToSkip = new List<Upload>();

        foreach (var upload in pendingUploads)
        {
            var hasNonExistingFiles = await HandleNonExistingArchiveFilesAsync(upload, cancellationToken);

            if (hasNonExistingFiles)
            {
                uploadsToSkip.Add(upload);
            }
        }

        return uploadsToSkip;
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

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await saveChangesSemaphore.WaitAsync(cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            saveChangesSemaphore.Release();
        }
    }

    private void DisposeSemaphores()
    {
        saveChangesSemaphore.Dispose();
        globalUploadSemaphore.Dispose();

        foreach (var semaphore in hosterUploadSemaphores.Values)
        {
            semaphore.Dispose();
        }
    }
}
