using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionPackingService(
    IDistributionReadRepository readRepository,
    IDistributionWriteRepository writeRepository,
    IEnumerable<IArchiver> archivers,
    ILogger<DistributionPackingService> logger)
{
    public async Task PackPendingDistributionsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Get pending distributions to pack");
        var distributionIds = await readRepository.GetDistributionIdsToPackAsync(cancellationToken);
        logger.LogInformation("Found {Count} distributions to pack", distributionIds.Count);
        
        foreach (var id in distributionIds)
        {
            try
            {
                await PackDistributionAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error packing distribution with Id {DistributionId}", id);
            }
        }
    }
    
    private async Task PackDistributionAsync(int distributionId, CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        var archiver = GetArchiverByFullClassName(distribution.ArchiveConfig.ArchiverFullClassName);
        
        logger.LogInformation("Start packing distribution {Name} with Id {DistributionId}",
            distributionId,
            distribution.Name);


        switch (distribution.Release.ReleaseType)
        {
            case ReleaseType.Unmanaged:
                await HandleUnmanagedDistributionAsync(
                    distribution: distribution,
                    archiveFileExtension: archiver.FileExtension,
                    cancellationToken: cancellationToken);
                break;
            case ReleaseType.Managed:
                await HandleManagedDistributionAsync(
                    distribution: distribution,
                    archiver: archiver,
                    cancellationToken: cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(distribution.Release.ReleaseType), distribution.Release.ReleaseType.ToString());
        }
    }
    
    private async Task HandleManagedDistributionAsync(
        UploadConfig distribution,
        IArchiver archiver,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start creating archives for distribution {Name} with Id {DistributionId} and archiver {Archiver}",
            distribution.Name,
            distribution.Id,
            archiver.Name);
        
        var archiveNamePrefix = distribution.ArchiveConfig.ArchiveNamePrefix
                            ?? throw new InvalidOperationException("Archive name prefix is required for managed distributions.");
        
        var targetFileSizeMb = distribution.ArchiveConfig.ArchiveFileSizeMb > 0
            ? distribution.ArchiveConfig.ArchiveFileSizeMb
            : throw new InvalidOperationException("Target archive file size must be greater than zero for managed distributions.");
        
        var archiveResult = await archiver.ArchiveAsync(
            sourceFolderPath: string.Empty,
            destinationPath: CreateTemporaryFolder(string.Empty),
            archiveNamePrefix: archiveNamePrefix,
            targetFileSizeMb: targetFileSizeMb,
            password: distribution.ArchiveConfig.ArchivePassword,
            cancellationToken: cancellationToken);

        if (!archiveResult.IsSuccess)
        {
            logger.LogError("Failed to create archives for distribution {Name} with Id {DistributionId}. Error: {ErrorMessage}",
                distribution.Name,
                distribution.Id,
                string.Join(", ", archiveResult.ErrorMessages!));

            return;
        }
        
        logger.LogInformation("Successfully created {ArchiveCount} archives for distribution {Name} with Id {DistributionId}",
            archiveResult.CreatedFileNames.Count,
            distribution.Name,
            distribution.Id);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleUnmanagedDistributionAsync(
        UploadConfig distribution,
        string archiveFileExtension,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling unmanaged distribution {Name} with Id {DistributionId}",
            distribution.Name,
            distribution.Id);
        
        var directoryInfo = new DirectoryInfo(string.Empty);
        var archiveFiles = directoryInfo.GetFiles($"*{archiveFileExtension}");
        
        logger.LogInformation("Found {ArchiveCount} archive files for unmanaged distribution {Name} with Id {DistributionId}",
            archiveFiles.Length,
            distribution.Name,
            distribution.Id);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }
    
    private IArchiver GetArchiverByFullClassName(string fullClassName)
    {
        return archivers.First(a => a.GetType().FullName == fullClassName);
    }
    
    private string CreateTemporaryFolder(string distributionFolderPath)
    {
        var tempFolderPath = Path.Combine(distributionFolderPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolderPath);
        return tempFolderPath;
    }
}
