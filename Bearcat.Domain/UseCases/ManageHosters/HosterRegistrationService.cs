using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;

namespace Bearcat.Domain.UseCases.ManageHosters;

public class HosterRegistrationService(
    IHosterConfigurationWriteRepository writeRepository,
    IHosterFactory hosterFactory
)
{
    public async Task<int> RegisterHosterAsync(
        string name,
        bool isActive,
        Dictionary<string, string> configuration,
        string hosterClassName,
        CancellationToken cancellationToken = default
    )
    {
        var hoster = hosterFactory.GetByName(hosterClassName);
        var serializedConfig = hoster.SerializeHosterConfig(configuration);

        var registration = new HosterRegistration
        {
            Name = name,
            IsActive = isActive,
            SerializedConfig = serializedConfig,
            HosterClassName = hosterClassName,
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
        return registration.Id;
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRegistrationAsync(
        int id,
        string name,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterFactory.GetByName(registration.HosterClassName);

        registration.Name = name;
        registration.SerializedConfig = hoster.SerializeHosterConfig(configuration);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterFactory.GetByName(registration.HosterClassName);
        var config = hoster.DeserializeHosterConfig(registration.SerializedConfig);
        return await hoster.TryLoginAsync(config, cancellationToken);
    }
}
