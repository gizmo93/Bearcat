using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.PixHost;
using Bearcat.ImageHosters.PixHost.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.PixHost;

public class PixHostTest
{
    private Mock<IPixHostApiClient> apiClientMock = null!;
    private ImageHosters.PixHost.PixHost service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IPixHostApiClient>(MockBehavior.Strict);
        service = new ImageHosters.PixHost.PixHost(
            apiClientMock.Object,
            NullLogger<ImageHosters.PixHost.PixHost>.Instance
        );
    }

    [Test]
    public async Task UploadImageAsync_ApiUploadsImage_ReturnsShowAndThumbnailUrls()
    {
        // Arrange
        var config = new PixHostConfig();
        var image = new ImageToUploadDto(
            Source: "https://example.test/cover.jpg",
            SourceType: ImageUploadSource.Url,
            Name: "cover"
        );

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, 0, CancellationToken.None))
            .ReturnsAsync(
                new UploadImageResponse
                {
                    Name = "cover.jpg",
                    ShowUrl = "https://pixhost.to/show/0/563_cover.jpg",
                    ThumbnailUrl = "https://t1.pixhost.to/thumbs/0/563_cover.jpg",
                }
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeEmpty();
        result.ImageUrls.ShouldBe([
            new ImageUrl(ImageSize.Full, "https://pixhost.to/show/0/563_cover.jpg"),
            new ImageUrl(ImageSize.Thumbnail, "https://t1.pixhost.to/thumbs/0/563_cover.jpg"),
        ]);
    }

    [Test]
    public async Task UploadImageAsync_ApiReturnsNoUrls_ReturnsError()
    {
        // Arrange
        var config = new PixHostConfig();
        var image = new ImageToUploadDto("https://example.test/cover.jpg", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, 0, CancellationToken.None))
            .ReturnsAsync(new UploadImageResponse());

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ImageUrls.ShouldBeEmpty();
        result.ErrorMessages.ShouldNotBeEmpty();
    }

    [Test]
    public async Task UploadImageAsync_ApiThrows_ReturnsErrorMessage()
    {
        // Arrange
        var config = new PixHostConfig();
        var image = new ImageToUploadDto("https://example.test/cover.jpg", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, 0, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("network down"));

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ImageUrls.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["network down"]);
    }
}
