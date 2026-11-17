using Bearcat.DistributionSites.GenericXenForo;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.GenericXenForo;

public class GenericXenForoTest
{
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
}
