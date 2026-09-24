using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.RemoteSource;
using FluentFTP;
using FluentFTP.BouncyCastle;

namespace Bearcat.RemoteSources.Ftp;

public sealed class FtpRemoteSource : IRemoteSource
{
    public const int DefaultPort = 21;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<ConfigurationField> Fields =
    [
        new(nameof(FtpRemoteSourceConfig.Host), ConfigurationFieldType.Text, IsRequired: true),
        new(
            nameof(FtpRemoteSourceConfig.Port),
            ConfigurationFieldType.Number,
            IsRequired: true,
            DefaultValue: DefaultPort
        ),
        new(nameof(FtpRemoteSourceConfig.Username), ConfigurationFieldType.Text, IsRequired: true),
        new(
            nameof(FtpRemoteSourceConfig.Password),
            ConfigurationFieldType.Password,
            IsRequired: true
        ),
        new(
            nameof(FtpRemoteSourceConfig.Encryption),
            ConfigurationFieldType.Select,
            IsRequired: true,
            DefaultValue: nameof(FtpEncryption.Explicit),
            Options: Enum.GetNames<FtpEncryption>()
        ),
        new(
            nameof(FtpRemoteSourceConfig.TlsProvider),
            ConfigurationFieldType.Select,
            IsRequired: true,
            DefaultValue: nameof(FtpTlsProvider.BouncyCastle),
            Options: Enum.GetNames<FtpTlsProvider>()
        ),
        new(
            nameof(FtpRemoteSourceConfig.ValidateCertificate),
            ConfigurationFieldType.Boolean,
            IsRequired: false,
            DefaultValue: false
        ),
    ];

    public string Name => "FTP / FTPS";

    public IReadOnlyList<ConfigurationField> ConfigurationFields => Fields;

    public IRemoteSourceConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<FtpRemoteSourceConfig>(
                serializedConfig,
                SerializerOptions
            ) ?? throw new JsonException("FTP remote source configuration is empty");
    }

    public async Task<IRemoteSourceSession> OpenSessionAsync(
        IRemoteSourceConfig config,
        CancellationToken cancellationToken
    )
    {
        var ftpConfig = (FtpRemoteSourceConfig)config;
        var client = new AsyncFtpClient(
            host: ftpConfig.Host,
            user: ftpConfig.Username,
            pass: ftpConfig.Password,
            port: ftpConfig.Port,
            config: CreateClientConfig(ftpConfig)
        );

        try
        {
            await client.Connect(cancellationToken);
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }

        return new FtpRemoteSourceSession(client);
    }

    public static FtpConfig CreateClientConfig(FtpRemoteSourceConfig config)
    {
        var clientConfig = new FtpConfig
        {
            EncryptionMode = config.Encryption switch
            {
                FtpEncryption.None => FtpEncryptionMode.None,
                FtpEncryption.Explicit => FtpEncryptionMode.Explicit,
                FtpEncryption.Implicit => FtpEncryptionMode.Implicit,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(config),
                    config.Encryption,
                    "Unknown FTP encryption mode"
                ),
            },
            DataConnectionEncryption = true,
            ValidateAnyCertificate = !config.ValidateCertificate,
            DisconnectWithQuit = false,
        };

        if (config.TlsProvider == FtpTlsProvider.BouncyCastle)
        {
            clientConfig.CustomStream = typeof(BouncyCastleFtpStream);
            clientConfig.CustomStreamConfig = new BouncyCastleFtpConfig
            {
                RequireSessionResumption = false,
            };
        }

        return clientConfig;
    }
}
