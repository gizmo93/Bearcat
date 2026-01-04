using BearCat.Core.Domain.Abstractions.Archiver;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;
using BearCat.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionPackingService(
    IDistributionCreationWriteRepository writeRepository,
    IEnumerable<IArchiver> archivers,
    ILogger<DistributionPackingService> logger)
{
    public async Task PackDistributionAsync(int distributionId, CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        var archiver = GetArchiverByFullClassName(distribution.ArchiverFullClassName);
        
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
        Distribution distribution,
        IArchiver archiver,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start creating archives for distribution {Name} with Id {DistributionId} and archiver {Archiver}",
            distribution.Name,
            distribution.Id,
            archiver.Name);
        
        var archiveNamePrefix = distribution.ArchiveNamePrefix
                            ?? throw new InvalidOperationException("Archive name prefix is required for managed distributions.");
        
        var targetFileSizeMb = distribution.TargetArchiveFileSizeMb > 0
            ? distribution.TargetArchiveFileSizeMb
            : throw new InvalidOperationException("Target archive file size must be greater than zero for managed distributions.");
        
        var archiveResult = await archiver.ArchiveAsync(
            sourceFolderPath: distribution.DistributionFolderPath,
            destinationPath: distribution.DistributionFolderPath,
            archiveNamePrefix: archiveNamePrefix,
            targetFileSizeMb: targetFileSizeMb,
            password: distribution.ArchivePassword,
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
        
        distribution.Archives.Add(new DistributionArchive
        {
            ArchiveFilePaths = archiveResult.CreatedFileNames.ToList(),
        });

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleUnmanagedDistributionAsync(
        Distribution distribution,
        string archiveFileExtension,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling unmanaged distribution {Name} with Id {DistributionId}",
            distribution.Name,
            distribution.Id);
        
        var directoryInfo = new DirectoryInfo(distribution.DistributionFolderPath);
        var archiveFiles = directoryInfo.GetFiles($"*{archiveFileExtension}");
        
        logger.LogInformation("Found {ArchiveCount} archive files for unmanaged distribution {Name} with Id {DistributionId}",
            archiveFiles.Length,
            distribution.Name,
            distribution.Id);
        
        distribution.Archives.Add(new DistributionArchive
        {
            ArchiveFilePaths = archiveFiles.Select(a => a.FullName).ToList(),
        });

        await writeRepository.SaveChangesAsync(cancellationToken);
    }
    
    private IArchiver GetArchiverByFullClassName(string fullClassName)
    {
        return archivers.First(a => a.GetType().FullName == fullClassName);
    }
}
