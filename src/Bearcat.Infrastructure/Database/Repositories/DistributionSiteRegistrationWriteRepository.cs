using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class DistributionSiteRegistrationWriteRepository(IBearcatWriteDbContext dbWrite)
    : IDistributionSiteRegistrationWriteRepository
{
    public async Task<DistributionSiteRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.DistributionSiteRegistrations.FirstAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public void Add(DistributionSiteRegistration registration)
    {
        dbWrite.DistributionSiteRegistrations.Add(registration);
    }

    public void Remove(DistributionSiteRegistration registration)
    {
        dbWrite.DistributionSiteRegistrations.Remove(registration);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
