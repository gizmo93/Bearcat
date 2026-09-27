using Bearcat.Infrastructure.Security;

namespace Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;

public sealed class FixedKeyProvider(byte fillValue) : IEncryptionKeyProvider
{
    private readonly byte[] key = Enumerable.Repeat(fillValue, 32).ToArray();

    public string KeyPath => "in-memory";

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public byte[] GetKey() => key;
}
