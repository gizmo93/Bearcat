using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.DistributionSites.BoerseCx;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.BoerseCx;

public class BoerseCxTest
{
    [Test]
    public void DeserializeConfig_JsonStoredBeforeConfigurationFields_ReadsCredentials()
    {
        // Arrange
        var site = new DistributionSites.BoerseCx.BoerseCx(Mock.Of<IHttpClientFactory>());

        // Act
        var config = site.DeserializeConfig("""{"Username":"uploader","Password":"secret"}""");

        // Assert
        var boerseCxConfig = config.ShouldBeOfType<BoerseCxConfig>();
        boerseCxConfig.Username.ShouldBe("uploader");
        boerseCxConfig.Password.ShouldBe("secret");
    }

    [Test]
    public void GetBaseUrl_AnyConfig_ReturnsFixedUrl()
    {
        // Arrange
        var site = new DistributionSites.BoerseCx.BoerseCx(Mock.Of<IHttpClientFactory>());
        var config = site.DeserializeConfig("""{"Username":"uploader","Password":"secret"}""");

        // Act
        var baseUrl = site.GetBaseUrl(config);

        // Assert
        baseUrl.ShouldBe("https://boerse.cx/");
    }

    [Test]
    public void ConfigurationFields_OnlyCredentials()
    {
        // Arrange
        var site = new DistributionSites.BoerseCx.BoerseCx(Mock.Of<IHttpClientFactory>());

        // Act
        var fields = site.ConfigurationFields;

        // Assert
        fields.ShouldBe([
            new ConfigurationField("Username", ConfigurationFieldType.Text, IsRequired: true),
            new ConfigurationField("Password", ConfigurationFieldType.Password, IsRequired: true),
        ]);
    }
}
