namespace Bearcat.Infrastructure.Security;

public interface IEncryptionKeyProvider
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    byte[] GetKey();

    string KeyPath { get; }
}
