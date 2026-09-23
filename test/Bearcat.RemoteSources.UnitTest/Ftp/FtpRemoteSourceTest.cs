using System.Text.Json;
using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.RemoteSources.Ftp;
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
    public void DeserializeConfig_KeysInDifferentCasing_MapsValues()
    {
        // Arrange
        const string json = """
            {"host":"ftp.example.test","port":990,"username":"user","password":"secret","encryption":"none"}
            """;

        // Act
        var config = (FtpRemoteSourceConfig)remoteSource.DeserializeConfig(json);

        // Assert
        config.Host.ShouldBe("ftp.example.test");
        config.Port.ShouldBe(990);
        config.Encryption.ShouldBe(FtpEncryption.None);
    }

    [Test]
    public void DeserializeConfig_UnknownEncryption_ThrowsJsonException()
    {
        // Arrange
        const string json = """
            {"Host":"ftp.example.test","Username":"user","Password":"secret","Encryption":"Sftp"}
            """;

        // Act
        var action = () => remoteSource.DeserializeConfig(json);

        // Assert
        action.ShouldThrow<JsonException>();
    }

    [Test]
    public void DeserializeConfig_RequiredValueMissing_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"Host":"ftp.example.test","Username":"user"}""";

        // Act
        var action = () => remoteSource.DeserializeConfig(json);

        // Assert
        action.ShouldThrow<JsonException>();
    }

    [Test]
    public void ToDictionary_Config_ReturnsTypedValues()
    {
        // Arrange
        var config = CreateConfig();

        // Act
        var values = config.ToDictionary();

        // Assert
        values["Host"].ShouldBe("ftp.example.test");
        values["Port"].ShouldBe(2121);
        values["Username"].ShouldBe("user");
        values["Password"].ShouldBe("secret");
        values["Encryption"].ShouldBe("Implicit");
        values["TlsProvider"].ShouldBe("System");
        values["ValidateCertificate"].ShouldBe(true);
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
    public void ConfigurationFields_DefinesTypesRequirementsAndDefaults()
    {
        // Act
        var fields = remoteSource.ConfigurationFields.ToDictionary(field => field.Key);

        // Assert
        fields["Host"].ShouldBe(new ConfigurationField("Host", ConfigurationFieldType.Text, true));
        fields["Port"]
            .ShouldBe(new ConfigurationField("Port", ConfigurationFieldType.Number, true, 21));
        fields["Username"]
            .ShouldBe(new ConfigurationField("Username", ConfigurationFieldType.Text, true));
        fields["Password"]
            .ShouldBe(new ConfigurationField("Password", ConfigurationFieldType.Password, true));
        fields["Encryption"].Type.ShouldBe(ConfigurationFieldType.Select);
        fields["Encryption"].IsRequired.ShouldBeTrue();
        fields["Encryption"].DefaultValue.ShouldBe("Explicit");
        fields["Encryption"].Options.ShouldBe(["None", "Explicit", "Implicit"]);
        fields["TlsProvider"].Type.ShouldBe(ConfigurationFieldType.Select);
        fields["TlsProvider"].IsRequired.ShouldBeTrue();
        fields["TlsProvider"].DefaultValue.ShouldBe("BouncyCastle");
        fields["TlsProvider"].Options.ShouldBe(["System", "BouncyCastle"]);
        fields["ValidateCertificate"]
            .ShouldBe(
                new ConfigurationField(
                    "ValidateCertificate",
                    ConfigurationFieldType.Boolean,
                    false,
                    false
                )
            );
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
