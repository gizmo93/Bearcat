using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Zippyshare;
using Bearcat.Hosters.Zippyshare.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Zippyshare;

public class ZippyshareTest
{
    private const string FileId = "x7k2m9q4w1z8c3v";

    private const string FileUrl = $"https://zippyshare.day/f/{FileId}";

    private static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            jsonSerializerOptions: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
            }
        ),
    };

    private static readonly ZippyshareConfig Config = new() { ApiKey = "zippyshare-api-key" };

    [Test]
    public async Task CheckFilesExistAsync_FileFound_ReturnsOnlineWithDownloadCount()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.OK,
            $$"""{"success":true,"file_id":"{{FileId}}","name":"release.part1.rar","size":104857600,"download_link":"{{FileUrl}}","downloads":17}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var result = await hoster.CheckFilesExistAsync(
            Config,
            [new FileUrlToCheckDto(FileUrl, ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[FileUrl].ShouldBeTrue();
        result.DownloadCountPerFileUrl![FileUrl].ShouldBe(17);
        handler.RequestUri!.AbsolutePath.ShouldBe($"/api/file/{FileId}");
    }

    [Test]
    public async Task CheckFilesExistAsync_MeasuredFileNotFoundResponse_ReturnsOffline()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.NotFound,
            """{"success":false,"error":"File not found."}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var result = await hoster.CheckFilesExistAsync(
            Config,
            [new FileUrlToCheckDto(FileUrl, ExternalId: FileId)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[FileUrl].ShouldBeFalse();
        result.DownloadCountPerFileUrl.ShouldBeEmpty();
    }

    [TestCase(HttpStatusCode.InternalServerError, """{"success":false,"error":"Server Error"}""")]
    [TestCase(HttpStatusCode.NotFound, "<html><body>Not Found</body></html>")]
    [TestCase(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
    [TestCase(HttpStatusCode.OK, "<html><body>Maintenance</body></html>")]
    public async Task CheckFilesExistAsync_UnmeasuredResponse_FailsWholeCheck(
        HttpStatusCode statusCode,
        string responseContent
    )
    {
        // Arrange
        var hoster = CreateHoster(new FixedResponseHandler(statusCode, responseContent));

        // Act
        var result = await hoster.CheckFilesExistAsync(
            Config,
            [new FileUrlToCheckDto(FileUrl, ExternalId: FileId)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
    }

    [Test]
    public async Task GetStorageQuotaAsync_StorageRemainingNull_ReturnsUnlimited()
    {
        // Arrange
        var apiClientMock = new Mock<IZippyshareApiClient>(MockBehavior.Strict);
        apiClientMock
            .Setup(x => x.GetUserAsync(Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new UserResponse(
                    Success: true,
                    Limits: new UserLimitsResponse(StorageTotal: null, StorageRemaining: null)
                )
            );
        var hoster = new Hosters.Zippyshare.Zippyshare(
            apiClientMock.Object,
            NullLogger<Hosters.Zippyshare.Zippyshare>.Instance
        );

        // Act
        var result = await hoster.GetStorageQuotaAsync(Config, CancellationToken.None);

        // Assert
        result.ShouldBe(HosterStorageQuota.Unlimited());
    }

    private static Hosters.Zippyshare.Zippyshare CreateHoster(HttpMessageHandler handler)
    {
        return new Hosters.Zippyshare.Zippyshare(
            new ApiClient(
                RestService.For<IZippyshareApi>(
                    new HttpClient(handler) { BaseAddress = new Uri(ApiClient.BaseUrl) },
                    RefitSettings
                ),
                new HttpClientProvider(new Mock<IHttpClientFactory>().Object)
            ),
            NullLogger<Hosters.Zippyshare.Zippyshare>.Instance
        );
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode, string responseContent)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseContent),
                    RequestMessage = request,
                }
            );
        }
    }
}
