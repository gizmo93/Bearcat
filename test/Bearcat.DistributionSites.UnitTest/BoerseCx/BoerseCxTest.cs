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
}
