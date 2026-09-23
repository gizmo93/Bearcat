using Bearcat.Abstractions.RemoteSource;

namespace Bearcat.RemoteSources.Ftp;

public sealed record FtpRemoteSourceConfig : IRemoteSourceConfig
{
    public required string Host { get; init; }

    public int Port { get; init; } = FtpRemoteSource.DefaultPort;

    public required string Username { get; init; }

    public required string Password { get; init; }

    public FtpEncryption Encryption { get; init; } = FtpEncryption.Explicit;

    public FtpTlsProvider TlsProvider { get; init; } = FtpTlsProvider.BouncyCastle;

    public bool ValidateCertificate { get; init; }

    public IReadOnlyDictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>
        {
            { nameof(Host), Host },
            { nameof(Port), Port },
            { nameof(Username), Username },
            { nameof(Password), Password },
            { nameof(Encryption), Encryption.ToString() },
            { nameof(TlsProvider), TlsProvider.ToString() },
            { nameof(ValidateCertificate), ValidateCertificate },
        };
    }
}
