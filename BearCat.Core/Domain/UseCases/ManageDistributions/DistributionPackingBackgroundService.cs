using BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Domain.UseCases.ManageDistributions;

public class DistributionPackingBackgroundService(
    DistributionPackingService distributionPackingService,
    IDistributionCreationReadRepository readRepository,
    ILogger<DistributionPackingBackgroundService> logger)
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
                await distributionPackingService.PackDistributionAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error packing distribution with Id {DistributionId}", id);
            }
        }
    }
}
