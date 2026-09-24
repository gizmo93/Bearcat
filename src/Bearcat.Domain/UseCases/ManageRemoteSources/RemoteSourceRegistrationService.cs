using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageRemoteSources;

public class RemoteSourceRegistrationService(
    IRemoteSourceRegistrationWriteRepository writeRepository,
    IRemoteSourceFactory remoteSourceFactory,
    ISecretProtector secretProtector,
    RemoteSourceSessionProvider sessionProvider,
    ILogger<RemoteSourceRegistrationService> logger
)
{
    public const int MinMaxConnections = 1;

    private static readonly TimeSpan SessionOperationTimeout = TimeSpan.FromSeconds(30);

    public async Task<int> CreateAsync(
        string name,
        string sourceClassName,
        IReadOnlyDictionary<string, object?> values,
        int maxConnections = RemoteSourceRegistration.DefaultMaxConnections,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedName = NormalizeName(name);
        ValidateMaxConnections(maxConnections);

        var remoteSource = remoteSourceFactory.GetByClassName(sourceClassName);
        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            remoteSource.ConfigurationFields,
            values
        );

        var registration = new RemoteSourceRegistration
        {
            Name = normalizedName,
            SourceClassName = sourceClassName,
            SerializedConfig = EncryptConfig(normalizedValues),
            IsActive = true,
            MaxConnections = maxConnections,
        };

        writeRepository.Add(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);

        return registration.Id;
    }

    public async Task UpdateAsync(
        int id,
        string name,
        IReadOnlyDictionary<string, object?> values,
        int maxConnections,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedName = NormalizeName(name);
        ValidateMaxConnections(maxConnections);

        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var remoteSource = remoteSourceFactory.GetByClassName(registration.SourceClassName);
        var normalizedValues = ConfigurationValueNormalizer.Normalize(
            remoteSource.ConfigurationFields,
            values,
            DecryptConfig(remoteSource, registration).ToDictionary()
        );

        registration.Name = normalizedName;
        registration.MaxConnections = maxConnections;
        registration.SerializedConfig = EncryptConfig(normalizedValues);

        await writeRepository.SaveChangesAsync(cancellationToken);
        await sessionProvider.CloseSessionsAsync(id);
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetConfigValuesWithoutSecretsAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var remoteSource = remoteSourceFactory.GetByClassName(registration.SourceClassName);
        var passwordKeys = remoteSource
            .ConfigurationFields.Where(field => field.Type == ConfigurationFieldType.Password)
            .Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);

        return DecryptConfig(remoteSource, registration)
            .ToDictionary()
            .Where(entry => !passwordKeys.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
        await sessionProvider.CloseSessionsAsync(id);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
        await sessionProvider.CloseSessionsAsync(id);
    }

    public async Task<RemoteSourceConnectionTestResult> TestConnectionAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);

        return await RunOnSessionWithTimeoutAsync(
            registration,
            "Connection test",
            async (session, token) =>
            {
                var folders = await session.ListFoldersAsync("/", token);

                return new RemoteSourceConnectionTestResult(
                    IsSuccess: true,
                    ErrorMessage: null,
                    RootFolderCount: folders.Count
                );
            },
            errorMessage => new RemoteSourceConnectionTestResult(
                IsSuccess: false,
                ErrorMessage: errorMessage,
                RootFolderCount: 0
            ),
            cancellationToken
        );
    }

    public async Task<RemoteFolderListingResult> ListFoldersAsync(
        int id,
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);

        return await RunOnSessionWithTimeoutAsync(
            registration,
            "Folder listing",
            async (session, token) =>
                new RemoteFolderListingResult(
                    IsSuccess: true,
                    ErrorMessage: null,
                    Folders: await session.ListFoldersAsync(path, token)
                ),
            errorMessage => new RemoteFolderListingResult(
                IsSuccess: false,
                ErrorMessage: errorMessage,
                Folders: []
            ),
            cancellationToken
        );
    }

    private async Task<TResult> RunOnSessionWithTimeoutAsync<TResult>(
        RemoteSourceRegistration registration,
        string operationName,
        Func<IRemoteSourceSession, CancellationToken, Task<TResult>> operation,
        Func<string, TResult> createFailure,
        CancellationToken cancellationToken
    )
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(SessionOperationTimeout);

        try
        {
            return await sessionProvider.UseSessionAsync(
                registration,
                session => operation(session, timeoutSource.Token),
                timeoutSource.Token
            );
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "{OperationName} for remote source {RemoteSourceName} timed out",
                operationName,
                registration.Name
            );

            return createFailure(
                $"The operation timed out after {SessionOperationTimeout.TotalSeconds:0} seconds."
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "{OperationName} for remote source {RemoteSourceName} failed",
                operationName,
                registration.Name
            );

            return createFailure(exception.Message);
        }
    }

    private IRemoteSourceConfig DecryptConfig(
        IRemoteSource remoteSource,
        RemoteSourceRegistration registration
    )
    {
        return remoteSource.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );
    }

    private string EncryptConfig(IReadOnlyDictionary<string, object?> values)
    {
        return secretProtector.Protect(ConfigurationValueSerializer.Serialize(values));
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.");
        }

        return name.Trim();
    }

    private static void ValidateMaxConnections(int maxConnections)
    {
        if (maxConnections < MinMaxConnections)
        {
            throw new ArgumentException($"Max connections must be at least {MinMaxConnections}.");
        }
    }
}
