using System.Net;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.ImageHosters.LoePic.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.ImageHosters.UnitTest.LoePic;

public class LoePicApiClientTest
{
    private const string ApiKey = "api-key";

    private Mock<ILoePicApi> apiMock = null!;
    private LoePicApiClient apiClient = null!;
    private string localFilePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiMock = new Mock<ILoePicApi>(MockBehavior.Strict);
        apiClient = new LoePicApiClient(apiMock.Object);
        localFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllTextAsync(localFilePath, "loepic test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(localFilePath);
    }

    [Test]
    public async Task UploadImageAsync_LocalFile_SendsMultipartFilePart()
    {
        // Arrange
        StreamPart? sentFile = null;

        apiMock
            .Setup(api =>
                api.UploadFileAsync(ApiKey, It.IsAny<StreamPart>(), CancellationToken.None)
            )
            .Callback<string, StreamPart, CancellationToken>((_, file, _) => sentFile = file)
            .ReturnsAsync(CreateEnvelope(new UploadResponse { Id = "aX3kF9mQp2" }));

        var image = new ImageToUploadDto(localFilePath, ImageUploadSource.LocalFile);

        // Act
        var response = await apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None);

        // Assert
        response.Id.ShouldBe("aX3kF9mQp2");
        sentFile.ShouldNotBeNull();
        sentFile.FileName.ShouldBe(Path.GetFileName(localFilePath));
        sentFile.ContentType.ShouldBe("image/png");
    }

    [Test]
    public async Task UploadImageAsync_Base64_SendsJsonBodyWithName()
    {
        // Arrange
        var base64 = Convert.ToBase64String([1, 2, 3, 4]);

        apiMock
            .Setup(api =>
                api.UploadBase64Async(
                    ApiKey,
                    new Base64UploadRequest(base64, "photo.png"),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(CreateEnvelope(new UploadResponse { Id = "aX3kF9mQp2" }));

        var image = new ImageToUploadDto(base64, ImageUploadSource.Base64, Name: "photo.png");

        // Act
        var response = await apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None);

        // Assert
        response.Id.ShouldBe("aX3kF9mQp2");
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadImageAsync_Base64WithoutName_OmitsName()
    {
        // Arrange
        var base64 = Convert.ToBase64String([1, 2, 3, 4]);

        apiMock
            .Setup(api =>
                api.UploadBase64Async(
                    ApiKey,
                    new Base64UploadRequest(base64, null),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(CreateEnvelope(new UploadResponse { Id = "aX3kF9mQp2" }));

        var image = new ImageToUploadDto(base64, ImageUploadSource.Base64);

        // Act
        await apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None);

        // Assert
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadImageAsync_Url_SendsUrlRequest()
    {
        // Arrange
        const string imageUrl = "https://example.test/photo.jpg";

        apiMock
            .Setup(api =>
                api.UploadUrlAsync(ApiKey, new UrlUploadRequest(imageUrl), CancellationToken.None)
            )
            .ReturnsAsync(
                CreateEnvelope(
                    new UploadResponse
                    {
                        Id = "aX3kF9mQp2",
                        Links = new UploadLinks
                        {
                            Direct = "https://www.loepic.me/i/aX3kF9mQp2.webp",
                        },
                    }
                )
            );

        var image = new ImageToUploadDto(imageUrl, ImageUploadSource.Url);

        // Act
        var response = await apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None);

        // Assert
        response.Links!.Direct.ShouldBe("https://www.loepic.me/i/aX3kF9mQp2.webp");
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadImageAsync_ResponseIsNotSuccessful_ThrowsWithApiErrorMessage()
    {
        // Arrange
        apiMock
            .Setup(api =>
                api.UploadUrlAsync(ApiKey, It.IsAny<UrlUploadRequest>(), CancellationToken.None)
            )
            .ReturnsAsync(
                CreateFailedEnvelope<UploadResponse>(HttpStatusCode.OK, "Unsupported image type")
            );

        var image = new ImageToUploadDto("https://example.test/photo.jpg", ImageUploadSource.Url);

        // Act
        var exception = await Should.ThrowAsync<LoePicApiException>(() =>
            apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("Unsupported image type");
    }

    [Test]
    public async Task UploadImageAsync_RequestFails_ThrowsWithStatusCodeAndApiErrorMessage()
    {
        // Arrange
        var envelope = await CreateErrorEnvelopeAsync<UploadResponse>(
            HttpStatusCode.Unauthorized,
            """{"success":false,"error":{"code":"unauthorized","message":"Invalid API key"}}"""
        );

        apiMock
            .Setup(api =>
                api.UploadUrlAsync(ApiKey, It.IsAny<UrlUploadRequest>(), CancellationToken.None)
            )
            .ReturnsAsync(envelope);

        var image = new ImageToUploadDto("https://example.test/photo.jpg", ImageUploadSource.Url);

        // Act
        var exception = await Should.ThrowAsync<LoePicApiException>(() =>
            apiClient.UploadImageAsync(ApiKey, image, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("401");
        exception.Message.ShouldContain("Invalid API key");
    }

    [Test]
    public async Task GetAccountAsync_ReturnsAccountDetails()
    {
        // Arrange
        apiMock
            .Setup(api => api.GetAccountAsync(ApiKey, CancellationToken.None))
            .ReturnsAsync(
                CreateEnvelope(
                    new AccountResponse
                    {
                        Username = "gizmo93",
                        Role = "user",
                        KeyLabel = "Bearcat",
                    }
                )
            );

        // Act
        var account = await apiClient.GetAccountAsync(ApiKey, CancellationToken.None);

        // Assert
        account.Username.ShouldBe("gizmo93");
        account.KeyLabel.ShouldBe("Bearcat");
        apiMock.VerifyAll();
    }

    private static ApiResponse<LoePicResponse<T>> CreateEnvelope<T>(T content)
    {
        return new ApiResponse<LoePicResponse<T>>(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            new LoePicResponse<T> { Success = true, Data = content },
            new RefitSettings(),
            error: null
        );
    }

    private static ApiResponse<LoePicResponse<T>> CreateFailedEnvelope<T>(
        HttpStatusCode statusCode,
        string message
    )
    {
        return new ApiResponse<LoePicResponse<T>>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            new LoePicResponse<T>
            {
                Success = false,
                Error = new LoePicError { Code = "invalid_image", Message = message },
            },
            new RefitSettings(),
            error: null
        );
    }

    private static async Task<ApiResponse<LoePicResponse<T>>> CreateErrorEnvelopeAsync<T>(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{LoePicApiClient.BaseUrl}/api/me");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var settings = new RefitSettings();
        var error = await ApiException.Create(request, HttpMethod.Post, response, settings);

        return new ApiResponse<LoePicResponse<T>>(response, default!, settings, error);
    }
}
