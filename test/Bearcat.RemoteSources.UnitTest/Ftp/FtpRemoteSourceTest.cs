using System.Text.Json;
using Bearcat.RemoteSources.Ftp;
using FluentFTP;
using FluentFTP.BouncyCastle;
using Shouldly;

namespace Bearcat.RemoteSources.UnitTest.Ftp;

public class FtpRemoteSourceTest
{
    private readonly FtpRemoteSource remoteSource = new();

    [Test]
    public void DeserializeConfig_JsonAsProducedByDomain_MapsTypedValues()
    {
        // Arrange
        const string json = """
            {
                "Host": "ftp.example.test",
                "Port": 2121,
                "Username": "user",
                "Password": "secret",
                "Encryption": "Implicit",
                "TlsProvider": "System",
                "ValidateCertificate": true
            }
            """;

        // Act
        var config = remoteSource.DeserializeConfig(json);

        // Assert
        config.ShouldBe(
            new FtpRemoteSourceConfig
            {
                Host = "ftp.example.test",
                Port = 2121,
                Username = "user",
                Password = "secret",
                Encryption = FtpEncryption.Implicit,
                TlsProvider = FtpTlsProvider.System,
                ValidateCertificate = true,
            }
        );
    }

    [Test]
    public void DeserializeConfig_OptionalValuesMissing_UsesDefaults()
    {
        // Arrange
        const string json = """{"Host":"ftp.example.test","Username":"user","Password":"secret"}""";

        // Act
        var config = (FtpRemoteSourceConfig)remoteSource.DeserializeConfig(json);

        // Assert
        config.Port.ShouldBe(21);
        config.Encryption.ShouldBe(FtpEncryption.Explicit);
        config.TlsProvider.ShouldBe(FtpTlsProvider.BouncyCastle);
        config.ValidateCertificate.ShouldBeFalse();
    }

    [Test]
    public void ToDictionary_SerializedAndDeserializedAgain_ReturnsEqualConfig()
    {
        // Arrange
        var config = CreateConfig();

        // Act
        var json = JsonSerializer.Serialize(config.ToDictionary());
        var deserializedConfig = remoteSource.DeserializeConfig(json);

        // Assert
        deserializedConfig.ShouldBe(config);
    }

    [Test]
    public void ConfigurationFields_KeysMatchConfigDictionaryKeys()
    {
        // Arrange
        var config = CreateConfig();

        // Act
        var fieldKeys = remoteSource.ConfigurationFields.Select(field => field.Key).ToList();

        // Assert
        fieldKeys.ShouldBe(config.ToDictionary().Keys.ToList());
    }

    [Test]
    public void DeserializeConfig_JsonBuiltFromFieldDefaults_UsesSameValuesAsConfigDefaults()
    {
        // Arrange
        var values = remoteSource.ConfigurationFields.ToDictionary(
            field => field.Key,
            field => field.DefaultValue
        );
        values["Host"] = "ftp.example.test";
        values["Username"] = "user";
        values["Password"] = "secret";

        // Act
        var config = remoteSource.DeserializeConfig(JsonSerializer.Serialize(values));

        // Assert
        config.ShouldBe(
            new FtpRemoteSourceConfig
            {
                Host = "ftp.example.test",
                Username = "user",
                Password = "secret",
            }
        );
    }

    [Test]
    public void CreateClientConfig_BouncyCastleProvider_DoesNotRequireSessionResumption()
    {
        // Arrange
        var config = CreateConfig() with
        {
            TlsProvider = FtpTlsProvider.BouncyCastle,
        };

        // Act
        var clientConfig = FtpRemoteSource.CreateClientConfig(config);

        // Assert
        clientConfig.CustomStream.ShouldBe(typeof(BouncyCastleFtpStream));
        clientConfig
            .CustomStreamConfig.ShouldBeOfType<BouncyCastleFtpConfig>()
            .RequireSessionResumption.ShouldBeFalse();
    }

    private static FtpRemoteSourceConfig CreateConfig()
    {
        return new FtpRemoteSourceConfig
        {
            Host = "ftp.example.test",
            Port = 2121,
            Username = "user",
            Password = "secret",
            Encryption = FtpEncryption.Implicit,
            TlsProvider = FtpTlsProvider.System,
            ValidateCertificate = true,
        };
    }
}
