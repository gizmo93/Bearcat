using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.ImgBb;
using Bearcat.ImageHosters.ImgBb.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.ImgBb;

public class ImgBbTest
{
    private Mock<IImgBbApiClient> apiClientMock = null!;
    private ImageHosters.ImgBb.ImgBb service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IImgBbApiClient>(MockBehavior.Strict);
        service = new ImageHosters.ImgBb.ImgBb(
            apiClientMock.Object,
            NullLogger<ImageHosters.ImgBb.ImgBb>.Instance
        );
    }

    [Test]
    public async Task UploadImageAsync_ApiUploadsImage_ReturnsAllImageUrls()
    {
        // Arrange
        var config = new ImgBbConfig { ApiKey = "api-key" };
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
                    Success = true,
                    Status = 200,
                    Data = new UploadData
                    {
                        Id = "image-id",
                        DeleteUrl = "https://ibb.co/delete",
                        Image = new UploadedImage { Url = "https://i.ibb.co/full.jpg" },
                        Thumbnail = new UploadedImage
                        {
                            Url = "https://i.ibb.co/thumb.jpg",
                        },
                        Medium = new UploadedImage { Url = "https://i.ibb.co/medium.jpg" },
                    },
                }
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ExternalId.ShouldBe("image-id");
        result.DeleteUrl.ShouldBe("https://ibb.co/delete");
        result.ErrorMessages.ShouldBeEmpty();
        result.ImageUrls.ShouldBe(
            [
                new ImageUrl(ImageSize.Full, "https://i.ibb.co/full.jpg"),
                new ImageUrl(ImageSize.Thumbnail, "https://i.ibb.co/thumb.jpg"),
                new ImageUrl(ImageSize.Medium, "https://i.ibb.co/medium.jpg"),
            ]
        );
    }

    [Test]
    public async Task UploadImageAsync_ApiFails_ReturnsErrorMessage()
    {
        // Arrange
        var config = new ImgBbConfig { ApiKey = "api-key" };
        var image = new ImageToUploadDto("invalid", ImageUploadSource.Url);

        apiClientMock
            .Setup(api => api.UploadImageAsync("api-key", image, CancellationToken.None))
            .ReturnsAsync(
                new UploadResponse
                {
                    Success = false,
                    Status = 400,
                    Error = new UploadError { Message = "Invalid API key." },
                }
            );

        // Act
        var result = await service.UploadImageAsync(image, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ImageUrls.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["Invalid API key."]);
    }

    [Test]
    public async Task TryLoginAsync_UploadsShortLivedTestImage()
    {
        // Arrange
        var config = new ImgBbConfig { ApiKey = "api-key" };
        Stream? uploadedImageStream = null;

        apiClientMock
            .Setup(api =>
                api.UploadImageAsync(
                    "api-key",
                    It.IsAny<Stream>(),
                    "bearcat-login-test.png",
                    "bearcat-login-test",
                    60,
                    CancellationToken.None
                )
            )
            .Callback<string, Stream, string, string?, int?, CancellationToken>(
                (_, imageStream, _, _, _, _) => uploadedImageStream = imageStream
            )
            .ReturnsAsync(
                new UploadResponse
                {
                    Success = true,
                    Status = 200,
                    Data = new UploadData
                    {
                        Image = new UploadedImage { Url = "https://i.ibb.co/full.gif" },
                    },
                }
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        uploadedImageStream.ShouldNotBeNull();
        uploadedImageStream.Length.ShouldBeGreaterThan(0);
    }
}
