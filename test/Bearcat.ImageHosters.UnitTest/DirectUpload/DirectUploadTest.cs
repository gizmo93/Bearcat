using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.DirectUpload;
using Bearcat.ImageHosters.DirectUpload.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.DirectUpload;

public class DirectUploadTest
{
    private Mock<IDirectUploadApiClient> apiClientMock = null!;
    private ImageHosters.DirectUpload.DirectUpload service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IDirectUploadApiClient>(MockBehavior.Strict);
        service = new ImageHosters.DirectUpload.DirectUpload(
            apiClientMock.Object,
            NullLogger<ImageHosters.DirectUpload.DirectUpload>.Instance
        );
    }

    [Test]
    public async Task UploadImageAsync_ApiUploadsImage_ReturnsDirectAndThumbnailUrls()
    {
        // Arrange
        var config = new DirectUploadConfig();
        var image = new ImageToUploadDto(
            Source: "https://example.test/cover.jpg",
            SourceType: ImageUploadSource.Url,
            Name: "cover"
        );

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, CancellationToken.None))
            .ReturnsAsync(
                new UploadResponse(
                    ImageId: "92037806",
                    DirectUrl: "https://s1.directupload.eu/images/260620/sf2opb5w.png",
                    ThumbnailUrl: "https://s1.directupload.eu/images/260620/temp/sf2opb5w.png",
                    DeleteUrl: "https://www.directupload.eu/delfile/Ly9XcW15djE3RVE9/"
                )
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeEmpty();
        result.ExternalId.ShouldBe("92037806");
        result.DeleteUrl.ShouldBe("https://www.directupload.eu/delfile/Ly9XcW15djE3RVE9/");
        result.ImageUrls.ShouldBe([
            new ImageUrl(ImageSize.Full, "https://s1.directupload.eu/images/260620/sf2opb5w.png"),
            new ImageUrl(
                ImageSize.Thumbnail,
                "https://s1.directupload.eu/images/260620/temp/sf2opb5w.png"
            ),
        ]);
    }

    [Test]
    public async Task UploadImageAsync_ResponseHasNoThumbnail_ReturnsOnlyDirectUrl()
    {
        // Arrange
        var config = new DirectUploadConfig();
        var image = new ImageToUploadDto("https://example.test/cover.jpg", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, CancellationToken.None))
            .ReturnsAsync(
                new UploadResponse(
                    ImageId: "1",
                    DirectUrl: "https://s1.directupload.eu/images/260620/sf2opb5w.png",
                    ThumbnailUrl: null,
                    DeleteUrl: null
                )
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ImageUrls.ShouldBe([
            new ImageUrl(ImageSize.Full, "https://s1.directupload.eu/images/260620/sf2opb5w.png"),
        ]);
    }

    [Test]
    public async Task UploadImageAsync_ApiThrows_ReturnsErrorMessage()
    {
        // Arrange
        var config = new DirectUploadConfig();
        var image = new ImageToUploadDto("https://example.test/cover.jpg", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync(image, CancellationToken.None))
            .ThrowsAsync(new DirectUploadApiException("directupload upload was rejected."));

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ImageUrls.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["directupload upload was rejected."]);
    }
}
