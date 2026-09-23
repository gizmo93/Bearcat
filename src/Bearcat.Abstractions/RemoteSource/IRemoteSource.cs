using Bearcat.Abstractions.ConfigurationFields;

namespace Bearcat.Abstractions.RemoteSource;

public interface IRemoteSource
{
    string Name { get; }

    IReadOnlyList<ConfigurationField> ConfigurationFields { get; }

    IRemoteSourceConfig DeserializeConfig(string serializedConfig);

    Task<IRemoteSourceSession> OpenSessionAsync(
        IRemoteSourceConfig config,
        CancellationToken cancellationToken
    );
}
