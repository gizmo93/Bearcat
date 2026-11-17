using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Bearcat.Hosters.Uploady;
using Bearcat.Hosters.Uploady.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Uploady;

public class UploadyTest
{
    private Mock<IUploadyApiClient> apiClientMock = null!;
    private Hosters.Uploady.Uploady service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IUploadyApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Uploady.Uploady>>();
        service = new Hosters.Uploady.Uploady(apiClientMock.Object, loggerMock.Object);
    }

    [Test]
    public async Task CheckFilesExistAsync_FileUrlWithHtmlExtension_MapsStatusToOriginalUrl()
    {
        // Arrange
        var config = new UploadyConfig { ApiKey = "api-key" };
        var fileUrl = "https://uploady.io/online-code.html";

        apiClientMock
            .Setup(x =>
                x.FilesExistAsync(
                    "api-key",
                    It.Is<IReadOnlySet<string>>(codes =>
                        codes.Count == 1 && codes.Contains("online-code")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, XFilesharingFileStatus>
                {
                    ["online-code"] = new(Exists: true, DownloadCount: null),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, null)],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrl].ShouldBeTrue();
        result.ErrorMessages.ShouldBeEmpty();
    }
}
