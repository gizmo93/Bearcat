using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class ModifyDistributionService(
    IDistributionCreationWriteRepository writeRepository)
{
    public async Task<int> CreateAsync(
        int releaseId,
        int hosterRegistrationId,
        string name,
        string archiverFullClassName,
        string? archivePassword,
        string? archiveNamePrefix,
        int targetArchiveFileSizeMb,
        string distributionFolderPath,
        CancellationToken cancellationToken)
    {
        var distribution = new Distribution
        {
            ReleaseId = releaseId,
            HosterRegistrationId = hosterRegistrationId,
            Name = name,
            ArchiverFullClassName = archiverFullClassName,
            ArchivePassword = archivePassword,
            ArchiveNamePrefix = archiveNamePrefix,
            TargetArchiveFileSizeMb = targetArchiveFileSizeMb,
            DistributionFolderPath = distributionFolderPath,
        };
        
        writeRepository.Add(distribution);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return distribution.Id;
    }
    
    public async Task UpdateAsync(
        int distributionId,
        string name,
        string? archivePassword,
        string? archiveNamePrefix,
        int targetArchiveFileSizeMb,
        string distributionFolderPath,
        CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        
        distribution.Name = name;
        distribution.ArchivePassword = archivePassword;
        distribution.ArchiveNamePrefix = archiveNamePrefix;
        distribution.TargetArchiveFileSizeMb = targetArchiveFileSizeMb;
        distribution.DistributionFolderPath = distributionFolderPath;
    }
    
    public async Task DeleteAsync(
        int distributionId,
        CancellationToken cancellationToken)
    {
        var distribution = await writeRepository.GetByIdAsync(distributionId, cancellationToken);
        writeRepository.Remove(distribution);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }
}
