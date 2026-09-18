using Bearcat.Hosters.HxFile.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.HxFile;

public class HxFileTest
{
    [Test]
    public void DownloadRequiresPremium_DirectLinksNeedPremiumViewPoint_IsTrue()
    {
        // Arrange
        var apiClientMock = new Mock<IHxFileApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.HxFile.HxFile>>();
        var service = new Hosters.HxFile.HxFile(apiClientMock.Object, loggerMock.Object);

        // Act
        var requiresPremium = service.DownloadRequiresPremium;

        // Assert
        requiresPremium.ShouldBeTrue();
    }
}
