using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.DistributionSites.GenericXenForo;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.GenericXenForo;

public class GenericXenForoTest
{
    private const string StoredConfig =
        """{"BaseUrl":"https://example.org/community/","Username":"uploader","Password":"secret"}""";

    [Test]
    public void DeserializeConfig_StoredJson_ReadsAllValues()
    {
        // Arrange
        var site = new DistributionSites.GenericXenForo.GenericXenForo(
            Mock.Of<IHttpClientFactory>()
        );

        // Act
        var config = site.DeserializeConfig(StoredConfig);

        // Assert
        var genericConfig = config.ShouldBeOfType<GenericXenForoConfig>();
        genericConfig.BaseUrl.ShouldBe("https://example.org/community/");
        genericConfig.Username.ShouldBe("uploader");
        genericConfig.Password.ShouldBe("secret");
    }

    [Test]
    public void DeserializeConfig_LowercasePropertyNames_ReadsAllValues()
    {
        // Arrange
        var site = new DistributionSites.GenericXenForo.GenericXenForo(
            Mock.Of<IHttpClientFactory>()
        );

        // Act
        var config = site.DeserializeConfig(
            """{"baseUrl":"https://example.org/","username":"uploader","password":"secret"}"""
        );

        // Assert
        var genericConfig = config.ShouldBeOfType<GenericXenForoConfig>();
        genericConfig.BaseUrl.ShouldBe("https://example.org/");
        genericConfig.Username.ShouldBe("uploader");
        genericConfig.Password.ShouldBe("secret");
    }

    [Test]
    public void GetBaseUrl_StoredConfig_ReturnsConfiguredBaseUrl()
    {
        // Arrange
        var site = new DistributionSites.GenericXenForo.GenericXenForo(
            Mock.Of<IHttpClientFactory>()
        );
        var config = site.DeserializeConfig(StoredConfig);

        // Act
        var baseUrl = site.GetBaseUrl(config);

        // Assert
        baseUrl.ShouldBe("https://example.org/community/");
    }

    [Test]
    public void ConfigurationFields_BaseUrlFollowedByCredentials()
    {
        // Arrange
        var site = new DistributionSites.GenericXenForo.GenericXenForo(
            Mock.Of<IHttpClientFactory>()
        );

        // Act
        var fields = site.ConfigurationFields;

        // Assert
        fields.ShouldBe([
            new ConfigurationField("BaseUrl", ConfigurationFieldType.Url, IsRequired: true),
            new ConfigurationField("Username", ConfigurationFieldType.Text, IsRequired: true),
            new ConfigurationField("Password", ConfigurationFieldType.Password, IsRequired: true),
        ]);
    }

    [Test]
    public void ToDictionary_StoredConfig_ContainsAllValues()
    {
        // Arrange
        var site = new DistributionSites.GenericXenForo.GenericXenForo(
            Mock.Of<IHttpClientFactory>()
        );
        var config = site.DeserializeConfig(StoredConfig);

        // Act
        var values = config.ToDictionary();

        // Assert
        values["BaseUrl"].ShouldBe("https://example.org/community/");
        values["Username"].ShouldBe("uploader");
        values["Password"].ShouldBe("secret");
    }
}
