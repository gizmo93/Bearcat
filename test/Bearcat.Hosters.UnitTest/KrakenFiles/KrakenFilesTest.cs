using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.KrakenFiles;
using Bearcat.Hosters.KrakenFiles.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.KrakenFiles;

public class KrakenFilesTest
{
    private Mock<IKrakenFilesApiClient> apiClientMock = null!;
    private Hosters.KrakenFiles.KrakenFiles service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IKrakenFilesApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.KrakenFiles.KrakenFiles>>();
        service = new Hosters.KrakenFiles.KrakenFiles(apiClientMock.Object, loggerMock.Object);
        service.UploadRetryDelay = TimeSpan.Zero;
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.EnsureApiKeyAcceptedAsync(config, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HosterCredentialsRejectedException("Invalid credentials."));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid credentials.");
    }
}
