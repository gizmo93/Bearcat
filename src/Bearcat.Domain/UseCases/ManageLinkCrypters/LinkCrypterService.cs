using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageProxyServers.Selection;

namespace Bearcat.Domain.UseCases.ManageLinkCrypters;

public class LinkCrypterService(
    ILinkCrypterRegistrationWriteRepository repository,
    ILinkCrypterRegistrationReadRepository readRepository,
    ILinkCrypterFactory linkCrypterFactory,
    ISecretProtector secretProtector,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService,
    ProxySelectionValidator proxySelectionValidator
)
{
    public async Task CreateAsync(
        string name,
        string className,
        IReadOnlyDictionary<string, string> configuration,
        ProxySelection proxySelection = ProxySelection.UseCategoryDefault,
        int? proxyServerId = null,
        CancellationToken cancellationToken = default
    )
    {
        var crypter = linkCrypterFactory.Get(className);

        var serializedConfig = crypter.SerializeConfig(configuration);

        var registration = new LinkCrypterRegistration
        {
            Name = name,
            LinkCrypterClassName = className,
            SerializedConfig = secretProtector.Protect(serializedConfig),
            IsActive = true,
            ProxySelection = proxySelection,
            ProxyServerId = await proxySelectionValidator.GetProxyServerIdToStoreAsync(
                proxySelection,
                proxyServerId,
                cancellationToken
            ),
        };

        repository.Add(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetLinkCrypterContainerCountAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        return await readRepository.GetLinkCrypterContainerCountAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        repository.Remove(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        string name,
        IReadOnlyDictionary<string, string> configuration,
        ProxySelection proxySelection = ProxySelection.UseCategoryDefault,
        int? proxyServerId = null,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        var crypter = linkCrypterFactory.Get(registration.LinkCrypterClassName);
        var mergedConfiguration = StoredConfigurationMerger.MergeSubmittedIntoStoredConfiguration(
            registration: registration,
            submittedConfiguration: configuration,
            deserializeStoredConfiguration: serializedConfig =>
                crypter.DeserializeConfig(serializedConfig).ToDictionary(),
            secretProtector: secretProtector
        );

        registration.Name = name;
        registration.ProxySelection = proxySelection;
        registration.ProxyServerId = await proxySelectionValidator.GetProxyServerIdToStoreAsync(
            proxySelection,
            proxyServerId,
            cancellationToken
        );
        registration.SerializedConfig = secretProtector.Protect(
            crypter.SerializeConfig(mergedConfiguration)
        );
        registration.HasUnreadableSecrets = false;

        await repository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        registration.IsActive = !registration.IsActive;

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        var crypter = linkCrypterFactory.Get(registration.LinkCrypterClassName);
        var config = crypter.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        using var proxyScope = LinkCrypterRegistrationProxyScope.Enter(registration);

        return await crypter.TryLoginAsync(config, cancellationToken);
    }
}
