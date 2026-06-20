using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

public interface IDistributionSiteRegistrationWriteRepository
{
    Task<DistributionSiteRegistration> GetByIdAsync(int id, CancellationToken cancellationToken);

    void Add(DistributionSiteRegistration registration);

    void Remove(DistributionSiteRegistration registration);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
