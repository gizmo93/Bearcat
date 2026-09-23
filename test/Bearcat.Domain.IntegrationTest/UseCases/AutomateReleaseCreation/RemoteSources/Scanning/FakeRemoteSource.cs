using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.RemoteSource;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public sealed class FakeRemoteSource(IReadOnlyDictionary<string, FakeRemoteServer> serversByKey)
    : IRemoteSource
{
    public string Name => "Fake remote source";

    public IReadOnlyList<ConfigurationField> ConfigurationFields { get; } = [];

    public IRemoteSourceConfig DeserializeConfig(string serializedConfig)
    {
        return new FakeRemoteSourceConfig(serializedConfig);
    }

    public Task<IRemoteSourceSession> OpenSessionAsync(
        IRemoteSourceConfig config,
        CancellationToken cancellationToken
    )
    {
        var server = serversByKey[((FakeRemoteSourceConfig)config).ServerKey];
        server.MarkOpened();

        if (server.OpenException is not null)
        {
            throw server.OpenException;
        }

        return Task.FromResult<IRemoteSourceSession>(new FakeRemoteSourceSession(server));
    }
}
