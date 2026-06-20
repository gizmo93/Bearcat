using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

public interface IDistributionSiteRegistrationReadRepository
{
    Task<IReadOnlyList<DistributionSiteRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );

    Task<DistributionSiteRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    );
}
