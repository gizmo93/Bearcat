using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageMediaDatabases.Repositories;

namespace Bearcat.Domain.UseCases.ManageMediaDatabases;

public class MediaDatabaseRegistrationService(
    IMediaDatabaseRegistrationWriteRepository repository,
    IMediaMetadataDatabaseFactory metadataDatabaseFactory,
    ISecretProtector secretProtector,
    UnreadableSecretsNotificationService unreadableSecretsNotificationService
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
                $"Media database {className} is already registered."
            );
        }

        var metadataDatabase = metadataDatabaseFactory.Get(className);
        var registration = new MediaDatabaseRegistration
        {
            MediaDatabaseClassName = className,
            SerializedConfig = secretProtector.Protect(
                metadataDatabase.SerializeConfig(configuration)
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
        var metadataDatabase = metadataDatabaseFactory.Get(registration.MediaDatabaseClassName);
        var mergedConfiguration = StoredConfigurationMerger.MergeSubmittedIntoStoredConfiguration(
            registration: registration,
            submittedConfiguration: configuration,
            deserializeStoredConfiguration: serializedConfig =>
                metadataDatabase.DeserializeConfig(serializedConfig).ToDictionary(),
            secretProtector: secretProtector
        );

        registration.SerializedConfig = secretProtector.Protect(
            metadataDatabase.SerializeConfig(mergedConfiguration)
        );
        registration.HasUnreadableSecrets = false;

        await repository.SaveChangesAsync(cancellationToken);
        await unreadableSecretsNotificationService.ResolveNotificationWhenNoUnreadableSecretsRemainAsync(
            cancellationToken
        );
    }

    public async Task<TryLoginResult> TryLoginAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(id, cancellationToken);

        var metadataDatabase = metadataDatabaseFactory.Get(registration.MediaDatabaseClassName);
        var config = metadataDatabase.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        return await metadataDatabase.TryLoginAsync(config, cancellationToken);
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
