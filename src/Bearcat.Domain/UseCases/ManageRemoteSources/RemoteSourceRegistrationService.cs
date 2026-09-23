using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageRemoteSources.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageRemoteSources;

public class RemoteSourceRegistrationService(
    IRemoteSourceRegistrationWriteRepository writeRepository,
    IRemoteSourceFactory remoteSourceFactory,
    ISecretProtector secretProtector,
    ILogger<RemoteSourceRegistrationService> logger
)
{
    public const int MinMaxConnections = 1;

    private static readonly TimeSpan ConnectionTestTimeout = TimeSpan.FromSeconds(30);

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
            SerializedConfig = Protect(normalizedValues),
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
            ReadConfig(remoteSource, registration).ToDictionary()
        );

        registration.Name = normalizedName;
        registration.MaxConnections = maxConnections;
        registration.SerializedConfig = Protect(normalizedValues);

        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetEditValuesAsync(
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

        return ReadConfig(remoteSource, registration)
            .ToDictionary()
            .Where(entry => !passwordKeys.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    public async Task ToggleIsActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        registration.IsActive = !registration.IsActive;
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        writeRepository.Remove(registration);
        await writeRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<RemoteSourceConnectionTestResult> TestConnectionAsync(
        int id,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await writeRepository.GetByIdAsync(id, cancellationToken);
        var remoteSource = remoteSourceFactory.GetByClassName(registration.SourceClassName);
        var config = ReadConfig(remoteSource, registration);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(ConnectionTestTimeout);

        try
        {
            await using var session = await remoteSource.OpenSessionAsync(
                config,
                timeoutSource.Token
            );
            var folders = await session.ListFoldersAsync("/", timeoutSource.Token);

            return new RemoteSourceConnectionTestResult(
                IsSuccess: true,
                ErrorMessage: null,
                RootFolderCount: folders.Count
            );
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Connection test for remote source {RemoteSourceName} timed out",
                registration.Name
            );

            return new RemoteSourceConnectionTestResult(
                IsSuccess: false,
                ErrorMessage: $"The connection test timed out after {ConnectionTestTimeout.TotalSeconds:0} seconds.",
                RootFolderCount: 0
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Connection test for remote source {RemoteSourceName} failed",
                registration.Name
            );

            return new RemoteSourceConnectionTestResult(
                IsSuccess: false,
                ErrorMessage: exception.Message,
                RootFolderCount: 0
            );
        }
    }

    private IRemoteSourceConfig ReadConfig(
        IRemoteSource remoteSource,
        RemoteSourceRegistration registration
    )
    {
        return remoteSource.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );
    }

    private string Protect(IReadOnlyDictionary<string, object?> values)
    {
        return secretProtector.Protect(ConfigurationValueSerializer.Serialize(values));
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static void ValidateMaxConnections(int maxConnections)
    {
        if (maxConnections < MinMaxConnections)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConnections),
                maxConnections,
                $"Max connections must be at least {MinMaxConnections}."
            );
        }
    }
}
