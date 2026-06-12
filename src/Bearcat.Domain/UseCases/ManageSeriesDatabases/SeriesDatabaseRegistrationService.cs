using Bearcat.Abstractions.Security;
using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageSeriesDatabases.Repositories;

namespace Bearcat.Domain.UseCases.ManageSeriesDatabases;

public class SeriesDatabaseRegistrationService(
    ISeriesDatabaseRegistrationWriteRepository repository,
    ISeriesDatabaseFactory seriesDatabaseFactory,
    ISecretProtector secretProtector
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
            throw new InvalidOperationException(
                $"Series database {className} is already registered."
            );
        }

        var seriesDatabase = seriesDatabaseFactory.Get(className);
        var registration = new SeriesDatabaseRegistration
        {
            SeriesDatabaseClassName = className,
            SerializedConfig = secretProtector.Protect(
                seriesDatabase.SerializeConfig(configuration)
            ),
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
        var seriesDatabase = seriesDatabaseFactory.Get(registration.SeriesDatabaseClassName);
        var serializedConfig = secretProtector.Unprotect(registration.SerializedConfig);
        var mergedConfiguration = new Dictionary<string, string>(
            seriesDatabase.DeserializeConfig(serializedConfig).ToDictionary()
        );

        foreach (var (key, value) in configuration)
        {
            mergedConfiguration[key] = value;
        }

        registration.SerializedConfig = secretProtector.Protect(
            seriesDatabase.SerializeConfig(mergedConfiguration)
        );

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);
        var seriesDatabase = seriesDatabaseFactory.Get(registration.SeriesDatabaseClassName);
        var config = seriesDatabase.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        return await seriesDatabase.TryLoginAsync(config, cancellationToken);
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
