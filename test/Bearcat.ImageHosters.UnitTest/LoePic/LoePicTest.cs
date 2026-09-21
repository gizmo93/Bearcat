using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.LoePic;
using Bearcat.ImageHosters.LoePic.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.LoePic;

public class LoePicTest
{
    private Mock<ILoePicApiClient> apiClientMock = null!;
    private ImageHosters.LoePic.LoePic service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<ILoePicApiClient>(MockBehavior.Strict);
        service = new ImageHosters.LoePic.LoePic(
            apiClientMock.Object,
            NullLogger<ImageHosters.LoePic.LoePic>.Instance
        );
    }

    [Test]
    public async Task UploadImageAsync_ApiUploadsImage_ReturnsFullImageUrl()
    {
        // Arrange
        var config = new LoePicConfig { ApiKey = "api-key" };
        var image = new ImageToUploadDto(
            Source: "https://example.test/cover.jpg",
            SourceType: ImageUploadSource.Url,
            Name: "cover"
        );

        apiClientMock
            .Setup(api => api.UploadImageAsync("api-key", image, CancellationToken.None))
            .ReturnsAsync(
                new UploadResponse
                {
                    Id = "aX3kF9mQp2",
                    Width = 1920,
                    Height = 1080,
                    Links = new UploadLinks
                    {
                        Direct = "https://www.loepic.me/i/aX3kF9mQp2.webp",
                        Page = "https://www.loepic.me/view/aX3kF9mQp2",
                    },
                }
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ExternalId.ShouldBe("aX3kF9mQp2");
        result.DeleteUrl.ShouldBeNull();
        result.ErrorMessages.ShouldBeEmpty();
        result.ImageUrls.ShouldBe([
            new ImageUrl(ImageSize.Full, "https://www.loepic.me/i/aX3kF9mQp2.webp"),
        ]);
    }

    [Test]
    public async Task UploadImageAsync_ApiThrows_ReturnsErrorMessage()
    {
        // Arrange
        var config = new LoePicConfig { ApiKey = "api-key" };
        var image = new ImageToUploadDto("invalid", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync("api-key", image, CancellationToken.None))
            .ThrowsAsync(new LoePicApiException("Invalid API key."));

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ImageUrls.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["Invalid API key."]);
    }

    [Test]
    public async Task TryLoginAsync_AccountLookupSucceeds_ReturnsSuccess()
    {
        // Arrange
        var config = new LoePicConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(api => api.GetAccountAsync("api-key", CancellationToken.None))
            .ReturnsAsync(new AccountResponse { Username = "gizmo93", Role = "user" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        apiClientMock.Verify(
            api => api.GetAccountAsync("api-key", CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task TryLoginAsync_AccountLookupFails_ReturnsErrorMessage()
    {
        // Arrange
        var config = new LoePicConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(api => api.GetAccountAsync("api-key", CancellationToken.None))
            .ThrowsAsync(new LoePicApiException("Unauthorized"));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Unauthorized");
    }
}
