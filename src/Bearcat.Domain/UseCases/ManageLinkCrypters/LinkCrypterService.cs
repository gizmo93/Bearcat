using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;

namespace Bearcat.Domain.UseCases.ManageLinkCrypters;

public class LinkCrypterService(
    ILinkCrypterRegistrationWriteRepository repository,
    ILinkCrypterFactory linkCrypterFactory,
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
        var crypter = linkCrypterFactory.Get(className);

        var serializedConfig = crypter.SerializeConfig(configuration);

        var registration = new LinkCrypterRegistration
        {
            Name = name,
            LinkCrypterClassName = className,
            SerializedConfig = secretProtector.Protect(serializedConfig),
            IsActive = true,
        };

        repository.Add(registration);
        await repository.SaveChangesAsync(cancellationToken);
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
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        var crypter = linkCrypterFactory.Get(registration.LinkCrypterClassName);
        var serializedConfig = secretProtector.Unprotect(registration.SerializedConfig);
        var mergedConfiguration = new Dictionary<string, string>(
            crypter.DeserializeConfig(serializedConfig).ToDictionary()
        );

        foreach (var (key, value) in configuration)
        {
            mergedConfiguration[key] = value;
        }

        registration.Name = name;
        registration.SerializedConfig = secretProtector.Protect(
            crypter.SerializeConfig(mergedConfiguration)
        );

        await repository.SaveChangesAsync(cancellationToken);
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

        return await crypter.TryLoginAsync(config, cancellationToken);
    }
}
