using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class DistributionSiteRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    IDistributionSiteFactory distributionSiteFactory
) : IDistributionSiteRegistrationReadRepository
{
    public async Task<IReadOnlyList<DistributionSiteRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var distributionSitesByClassName = DistributionSitesByClassName();

        var registrations = await dbRead
            .DistributionSiteRegistrations.OrderBy(registration => registration.Name)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.DistributionSiteClassName,
                registration.IsActive,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration =>
                ToReadModel(
                    id: registration.Id,
                    name: registration.Name,
                    className: registration.DistributionSiteClassName,
                    isActive: registration.IsActive,
                    distributionSitesByClassName: distributionSitesByClassName
                )
            )
            .ToList();
    }

    public async Task<DistributionSiteRegistrationReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await dbRead
            .DistributionSiteRegistrations.Where(registration => registration.Id == id)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.DistributionSiteClassName,
                registration.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registration is null)
        {
            return null;
        }

        return ToReadModel(
            id: registration.Id,
            name: registration.Name,
            className: registration.DistributionSiteClassName,
            isActive: registration.IsActive,
            distributionSitesByClassName: DistributionSitesByClassName()
        );
    }

    private IReadOnlyDictionary<string, DistributionSiteDto> DistributionSitesByClassName()
    {
        return distributionSiteFactory
            .GetDistributionSites()
            .ToDictionary(distributionSite => distributionSite.ClassName);
    }

    private static DistributionSiteRegistrationReadModel ToReadModel(
        int id,
        string name,
        string className,
        bool isActive,
        IReadOnlyDictionary<string, DistributionSiteDto> distributionSitesByClassName
    )
    {
        var distributionSite = distributionSitesByClassName[className];

        return new DistributionSiteRegistrationReadModel(
            DistributionSiteRegistrationId: id,
            Name: name,
            DistributionSiteClassName: className,
            DistributionSiteName: distributionSite.Name,
            Kind: distributionSite.Kind,
            IsActive: isActive
        );
    }
}
