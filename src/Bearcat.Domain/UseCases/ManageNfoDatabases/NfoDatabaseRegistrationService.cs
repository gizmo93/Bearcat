using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNfoDatabases.Repositories;

namespace Bearcat.Domain.UseCases.ManageNfoDatabases;

public class NfoDatabaseRegistrationService(
    INfoDatabaseRegistrationWriteRepository repository,
    INfoDatabaseFactory nfoDatabaseFactory
)
{
    public async Task CreateAsync(
        string className,
        IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken = default
    )
    {
        if (
            await repository.ExistsForClassNameAsync(
                className,
                cancellationToken: cancellationToken
            )
        )
        {
            throw new InvalidOperationException($"NFO database {className} is already registered.");
        }

        var nfoDatabase = nfoDatabaseFactory.Get(className);
        var registration = new NfoDatabaseRegistration
        {
            NfoDatabaseClassName = className,
            SerializedConfig = nfoDatabase.SerializeConfig(configuration),
            IsActive = true,
        };

        repository.Add(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int id,
        IReadOnlyDictionary<string, string> configuration,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);

        registration.SerializedConfig = nfoDatabase.SerializeConfig(configuration);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        repository.Remove(registration);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
