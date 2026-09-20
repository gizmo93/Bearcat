using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ManageDistributionSites.ReadModels;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class DistributionSiteRegistrationReadRepository(
    IBearcatReadDbContext dbRead,
    IDistributionSiteFactory distributionSiteFactory,
    ISecretProtector secretProtector
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
                registration.SerializedConfig,
                registration.IsActive,
                registration.EnableAutomaticPosting,
                registration.StripDotsForThreadSearch,
                PostingRuleCount = registration.PostingRules.Count,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration =>
                ToReadModel(
                    id: registration.Id,
                    name: registration.Name,
                    className: registration.DistributionSiteClassName,
                    serializedConfig: registration.SerializedConfig,
                    isActive: registration.IsActive,
                    enableAutomaticPosting: registration.EnableAutomaticPosting,
                    stripDotsForThreadSearch: registration.StripDotsForThreadSearch,
                    postingRuleCount: registration.PostingRuleCount,
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
                registration.SerializedConfig,
                registration.IsActive,
                registration.EnableAutomaticPosting,
                registration.StripDotsForThreadSearch,
                PostingRuleCount = registration.PostingRules.Count,
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
            serializedConfig: registration.SerializedConfig,
            isActive: registration.IsActive,
            enableAutomaticPosting: registration.EnableAutomaticPosting,
            stripDotsForThreadSearch: registration.StripDotsForThreadSearch,
            postingRuleCount: registration.PostingRuleCount,
            distributionSitesByClassName: DistributionSitesByClassName()
        );
    }

    private IReadOnlyDictionary<string, DistributionSiteDto> DistributionSitesByClassName()
    {
        return distributionSiteFactory
            .GetDistributionSites()
            .ToDictionary(distributionSite => distributionSite.ClassName);
    }

    private DistributionSiteRegistrationReadModel ToReadModel(
        int id,
        string name,
        string className,
        string serializedConfig,
        bool isActive,
        bool enableAutomaticPosting,
        bool stripDotsForThreadSearch,
        int postingRuleCount,
        IReadOnlyDictionary<string, DistributionSiteDto> distributionSitesByClassName
    )
    {
        var distributionSite = distributionSitesByClassName[className];
        var site = distributionSiteFactory.Get(className);
        var config = site.DeserializeConfig(secretProtector.Unprotect(serializedConfig));
        var configuredSite = site.WithConfiguration(config);
        var configurationValues = config.ToDictionary();
        var editableConfiguration = distributionSite
            .ConfigurationFields.Where(field => field.PrefillOnEdit && !field.IsSecret)
            .ToDictionary(field => field.Key, field => configurationValues[field.Key]);

        return new DistributionSiteRegistrationReadModel(
            DistributionSiteRegistrationId: id,
            Name: name,
            DistributionSiteClassName: className,
            DistributionSiteName: distributionSite.Name,
            Kind: distributionSite.Kind,
            IsActive: isActive,
            EnableAutomaticPosting: enableAutomaticPosting,
            StripDotsForThreadSearch: stripDotsForThreadSearch,
            PostingRuleCount: postingRuleCount,
            BaseUrl: configuredSite.BaseUrl,
            Configuration: editableConfiguration
        );
    }
}
