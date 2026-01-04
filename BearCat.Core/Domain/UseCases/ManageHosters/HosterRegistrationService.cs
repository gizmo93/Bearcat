using BearCat.Core.Domain.Abstractions.Hoster.Results;
using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageHosters.Repositories;

namespace BearCat.Core.Domain.UseCases.ManageHosters;

public class HosterRegistrationService(
    IHosterConfigurationWriteRepository writeRepository,
    HosterInstanceService hosterInstanceService)
{
    public async Task<int> RegisterHosterAsync(
        string name,
        bool isActive,
        string serializedConfig,
        string hosterClassName,
        CancellationToken cancellationToken)
    {
        var registration = new HosterRegistration
        {
            Name = name,
            IsActive = isActive,
            SerializedConfig = serializedConfig,
            HosterFullClassName = hosterClassName
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
        return registration.Id;
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRegistrationAsync(
        int id,
        string name,
        string serializedConfig,
        CancellationToken cancellationToken)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.Name = name;
        registration.SerializedConfig = serializedConfig;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var hoster = hosterInstanceService.GetByFullClassName(registration.HosterFullClassName);
        var config = hoster.DeserializeHosterConfig(registration.SerializedConfig);
        return await hoster.TryLoginAsync(config, cancellationToken);
    }
}
