namespace BearCat.Core.Domain.UseCases.ManageDistributions.Repositories;

public interface IDistributionReadRepository
{
    Task<IReadOnlyList<int>> GetDistributionIdsToPackAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<int>> GetDistributionIdsToUploadAsync(CancellationToken cancellationToken);
}
