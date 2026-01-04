using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionUploadBackgroundService(
    DistributionUploadService distributionUploadService,
    IDistributionCreationReadRepository readRepository,
    ILogger<DistributionUploadBackgroundService> logger)
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
                await distributionUploadService.UploadDistributionAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading distribution with Id {DistributionId}", id);
            }
        }
    }
}
