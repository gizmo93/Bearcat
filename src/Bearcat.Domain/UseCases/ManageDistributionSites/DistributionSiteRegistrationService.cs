using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

namespace Bearcat.Domain.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationService(
    IDistributionSiteRegistrationWriteRepository repository,
    IDistributionSiteRegistrationReadRepository readRepository,
    IDistributionSiteFactory distributionSiteFactory,
    ISecretProtector secretProtector,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService
)
{
    public async Task CreateAsync(
        string name,
        string className,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default
    )
    {
        var distributionSite = distributionSiteFactory.GetByClassName(className);
        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            distributionSite.ConfigurationFields,
            values
        );

        var registration = new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = className,
            SerializedConfig = EncryptConfig(normalizedValues),
            IsActive = true,
        };

        repository.Add(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        var distributionSite = distributionSiteFactory.GetByClassName(
            registration.DistributionSiteClassName
        );
        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            fields: distributionSite.ConfigurationFields,
            submittedValues: values,
            existingValues: registration.HasUnreadableSecrets
                ? null
                : DecryptConfig(distributionSite, registration).ToDictionary()
        );

        registration.Name = name;
        registration.SerializedConfig = EncryptConfig(normalizedValues);
        registration.HasUnreadableSecrets = false;
        registration.EncryptedSession = null;

        await repository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetConfigValuesWithoutSecretsAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        var distributionSite = distributionSiteFactory.GetByClassName(
            registration.DistributionSiteClassName
        );
        var passwordKeys = distributionSite
            .ConfigurationFields.Where(field => field.Type == ConfigurationFieldType.Password)
            .Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);

        return DecryptConfig(distributionSite, registration)
            .ToDictionary()
            .Where(entry => !passwordKeys.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetBaseUrlsByRegistrationIdAsync(
        IReadOnlyList<int> registrationIds,
        CancellationToken cancellationToken = default
    )
    {
        var registrations = await repository.GetByIdsAsync(registrationIds, cancellationToken);

        return registrations
            .Where(registration => !registration.HasUnreadableSecrets)
            .ToDictionary(registration => registration.Id, GetBaseUrl);
    }

    public async Task<int> GetForumPostingRuleCountAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await readRepository.GetForumPostingRuleCountAsync(id, cancellationToken);
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

    public async Task SetAutomaticPostingAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        registration.EnableAutomaticPosting = isEnabled;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStripDotsForThreadSearchAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        registration.StripDotsForThreadSearch = isEnabled;

        await repository.SaveChangesAsync(cancellationToken);
    }

    private string GetBaseUrl(DistributionSiteRegistration registration)
    {
        var distributionSite = distributionSiteFactory.GetByClassName(
            registration.DistributionSiteClassName
        );
        return distributionSite.GetBaseUrl(DecryptConfig(distributionSite, registration));
    }

    private IDistributionSiteConfig DecryptConfig(
        IDistributionSite distributionSite,
        DistributionSiteRegistration registration
    )
    {
        return distributionSite.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );
    }

    private string EncryptConfig(IReadOnlyDictionary<string, object?> values)
    {
        return secretProtector.Protect(ConfigurationValueSerializer.Serialize(values));
    }
}
