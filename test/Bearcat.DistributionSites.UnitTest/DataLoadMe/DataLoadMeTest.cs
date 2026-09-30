using Bearcat.DistributionSites.DataLoadMe;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.DataLoadMe;

public class DataLoadMeTest
{
    [Test]
    public void DeserializeConfig_JsonStoredBeforeConfigurationFields_ReadsCredentials()
    {
        // Arrange
        var site = new DistributionSites.DataLoadMe.DataLoadMe(Mock.Of<IHttpClientFactory>());

        // Act
        var config = site.DeserializeConfig("""{"Username":"uploader","Password":"secret"}""");

        // Assert
        var dataLoadMeConfig = config.ShouldBeOfType<DataLoadMeConfig>();
        dataLoadMeConfig.Username.ShouldBe("uploader");
        dataLoadMeConfig.Password.ShouldBe("secret");
    }

    [Test]
    public void GetBaseUrl_AnyConfig_ReturnsFixedUrl()
    {
        // Arrange
        var site = new DistributionSites.DataLoadMe.DataLoadMe(Mock.Of<IHttpClientFactory>());
        var config = site.DeserializeConfig("""{"Username":"uploader","Password":"secret"}""");

        // Act
        var baseUrl = site.GetBaseUrl(config);

        // Assert
        baseUrl.ShouldBe("https://www.data-load.me/");
    }
}
