using Bearcat.Abstractions.RemoteSource;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources;

public sealed class FakeRemoteSourceConfig(string serverKey) : IRemoteSourceConfig
{
    public string ServerKey { get; } = serverKey;

    public IReadOnlyDictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?> { ["ServerKey"] = ServerKey };
    }
}
