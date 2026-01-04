using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionUploadService(
    IDistributionCreationWriteRepository writeRepository,
    HosterInstanceService hosterInstanceService,
    ILogger<DistributionUploadService> logger)
{
    public async Task UploadDistributionAsync(int distributionId, CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        var hoster = hosterInstanceService.GetByFullClassName(distribution.HosterRegistration.HosterFullClassName);
        var hosterConfig = hoster.DeserializeHosterConfig(distribution.HosterRegistration.SerializedConfig);
        
        logger.LogInformation("Start uploading distribution {Name} with Id {DistributionId} to hoster {Hoster}",
            distribution.Name,
            distribution.Id,
            hoster.Name);
        
        var archivesToUpload = distribution.Archives
            .Where(a => a.ArchiveUpload is null)
            .ToList();

        foreach (var archive in archivesToUpload)
        {
            await UploadArchiveAsync(
                archive: archive,
                hoster: hoster,
                hosterConfig: hosterConfig,
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task UploadArchiveAsync(
        DistributionArchive archive,
        IHoster hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start uploading archive {ArchiveId} of distribution {DistributionId} to hoster {Hoster}",
            archive.Id,
            archive.DistributionId,
            hoster.Name);

        await hoster.PrepareForUploadAsync(hosterConfig, cancellationToken);

        archive.ArchiveUpload = new ArchiveUpload
        {
            State = ArchiveUploadState.Uploading,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await writeRepository.SaveChangesAsync(cancellationToken);

        var uploadResults = await ProcessUploadAsync(
            archive: archive,
            hoster: hoster,
            hosterConfig: hosterConfig,
            cancellationToken: cancellationToken);
        
        var failedUploads = uploadResults
            .Where(r => !r.IsSuccess)
            .ToList();

        if (failedUploads.Count > 0)
        {
            logger.LogError("Failed to upload the following files for archive {ArchiveId} of distribution {DistributionId} to hoster {Hoster}: {FailedFiles}",
                archive.Id,
                archive.DistributionId,
                hoster.Name,
                string.Join(", ", failedUploads.Select(f => f.SourceFilePath)));
        }
        else
        {
            logger.LogInformation("Successfully uploaded archive {ArchiveId} of distribution {DistributionId} to hoster {Hoster}",
                archive.Id,
                archive.DistributionId,
                hoster.Name);
        }

        archive.ArchiveUpload.UpdatedAt = DateTime.UtcNow;
        archive.ArchiveUpload.State = failedUploads.Count > 0
            ? ArchiveUploadState.Failed
            : ArchiveUploadState.Completed;
        archive.ArchiveUpload.HosterFiles = uploadResults
            .Select(r => new HosterFile
            {
                SourceFileName = r.SourceFilePath,
                FileUrl = r.FileUrl ?? string.Empty,
                State = r.IsSuccess
                    ? HosterFileState.Online
                    : HosterFileState.UploadFailed,
            })
            .ToList();
        
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<UploadFileResult>> ProcessUploadAsync(
        DistributionArchive archive,
        IHoster hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        var numberOfParallelUploads = await hoster.GetMaximumParallelUploadsAsync(
            hosterConfig: hosterConfig,
            cancellationToken: cancellationToken) ?? 1;
        
        var semaphore = new SemaphoreSlim(numberOfParallelUploads);

        var uploadTasks = archive.ArchiveFilePaths
            .OrderBy(f => f)
            .Select(async f =>
            {
                try
                {
                    await semaphore.WaitAsync(cancellationToken);
                    
                    logger.LogInformation("Uploading file {FilePath} of archive {ArchiveId} to hoster {Hoster}",
                        f,
                        archive.Id,
                        hoster.Name);
                    
                    return await hoster.UploadFileAsync(
                        hosterConfig: hosterConfig,
                        fullFilePath: f,
                        cancellationToken: cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();
        
        return await Task.WhenAll(uploadTasks);
    }
}
