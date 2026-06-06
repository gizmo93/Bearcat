using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;

namespace Bearcat.Domain.UseCases.ManageImageHosters;

public class ImageHosterService(
    IImageHosterRegistrationWriteRepository repository,
    IImageHosterFactory imageHosterFactory,
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
        var imageHoster = imageHosterFactory.Get(className);
        var serializedConfig = imageHoster.SerializeConfig(configuration);

        var registration = new ImageHosterRegistration
        {
            Name = name,
            ImageHosterClassName = className,
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
        var imageHoster = imageHosterFactory.Get(registration.ImageHosterClassName);
        var serializedConfig = secretProtector.Unprotect(registration.SerializedConfig);
        var mergedConfiguration = new Dictionary<string, string>(
            imageHoster.DeserializeConfig(serializedConfig).ToDictionary()
        );

        foreach (var (key, value) in configuration)
        {
            mergedConfiguration[key] = value;
        }

        registration.Name = name;
        registration.SerializedConfig = secretProtector.Protect(
            imageHoster.SerializeConfig(mergedConfiguration)
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
        var imageHoster = imageHosterFactory.Get(registration.ImageHosterClassName);
        var config = imageHoster.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        return await imageHoster.TryLoginAsync(config, cancellationToken);
    }
}
