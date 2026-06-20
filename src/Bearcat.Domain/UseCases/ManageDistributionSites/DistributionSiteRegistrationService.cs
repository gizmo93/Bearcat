using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

namespace Bearcat.Domain.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationService(
    IDistributionSiteRegistrationWriteRepository repository,
    IDistributionSiteFactory distributionSiteFactory,
    ISecretProtector secretProtector
)
{
    public async Task CreateAsync(
        string name,
        string className,
        IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken = default
    )
    {
        var distributionSite = distributionSiteFactory.Get(className);
        var serializedConfig = distributionSite.SerializeConfig(
            new Dictionary<string, string>(configuration)
        );

        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = className,
            SerializedConfig = secretProtector.Protect(serializedConfig),
            IsActive = true,
        };

        repository.Add(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        var distributionSite = distributionSiteFactory.Get(registration.DistributionSiteClassName);

        var mergedConfiguration = new Dictionary<string, string>(
            distributionSite
                .DeserializeConfig(secretProtector.Unprotect(registration.SerializedConfig))
                .ToDictionary()
        );

        foreach (var (key, value) in configuration)
        {
            mergedConfiguration[key] = value;
        }

        registration.Name = name;
        registration.SerializedConfig = secretProtector.Protect(
            distributionSite.SerializeConfig(mergedConfiguration)
        );
        registration.EncryptedSession = null;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        repository.Remove(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        registration.IsActive = !registration.IsActive;

        await repository.SaveChangesAsync(cancellationToken);
    }
}
