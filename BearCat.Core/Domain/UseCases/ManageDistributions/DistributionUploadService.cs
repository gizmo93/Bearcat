using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionUploadService(
    IDistributionReadRepository readRepository,
    IDistributionWriteRepository writeRepository,
    HosterInstanceService hosterInstanceService,
    ILogger<DistributionUploadService> logger)
{
    public async Task UploadPendingDistributionsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Get pending distributions to upload");
        var distributionIds = await readRepository.GetDistributionIdsToUploadAsync(cancellationToken);
        logger.LogInformation("Found {Count} distributions to upload", distributionIds.Count);
        
        foreach (var id in distributionIds)
        {
            try
            {
                await UploadDistributionAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading distribution with Id {DistributionId}", id);
            }
        }
    }
    
    private async Task UploadDistributionAsync(int distributionId, CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        var hoster = hosterInstanceService.GetByFullClassName(distribution.HosterRegistration.HosterFullClassName);
        var hosterConfig = hoster.DeserializeHosterConfig(distribution.HosterRegistration.SerializedConfig);
        
        logger.LogInformation("Start uploading distribution {Name} with Id {DistributionId} to hoster {Hoster}",
            distribution.Name,
            distribution.Id,
            hoster.Name);
        
        var archivesToUpload = distribution.Uploads
            .Where(a => a.UploadState == UploadState.Pending)
            .ToList();

        foreach (var archive in archivesToUpload)
        {
            await UploadArchiveAsync(
                archive: new Archive(),
                hoster: hoster,
                hosterConfig: hosterConfig,
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task UploadArchiveAsync(
        Archive archive,
        IHoster hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start uploading archive {ArchiveId} of distribution {DistributionId} to hoster {Hoster}",
            archive.Id,
            archive.Id,
            hoster.Name);

        await hoster.PrepareForUploadAsync(hosterConfig, cancellationToken);
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
                archive.ArchiveConfigId,
                hoster.Name,
                string.Join(", ", failedUploads.Select(f => f.SourceFilePath)));
        }
        else
        {
            logger.LogInformation("Successfully uploaded archive {ArchiveId} of distribution {DistributionId} to hoster {Hoster}",
                archive.Id,
                archive.ArchiveConfigId,
                hoster.Name);
        }
        
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<UploadFileResult>> ProcessUploadAsync(
        Archive archive,
        IHoster hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken)
    {
        var numberOfParallelUploads = await hoster.GetMaximumParallelUploadsAsync(
            hosterConfig: hosterConfig,
            cancellationToken: cancellationToken) ?? 1;
        
        var semaphore = new SemaphoreSlim(numberOfParallelUploads);

        var uploadTasks = archive.ArchiveFiles
            .OrderBy(f => f.FullFileName)
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
                        fullFilePath: f.FullFileName,
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
