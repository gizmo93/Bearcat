using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.FileQ.Api;
using Bearcat.Hosters.FileServe.Api;
using Bearcat.Hosters.HxFile.Api;
using Bearcat.Hosters.Mega4Upload.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Refit;
using Shouldly;
using FileQApiClient = Bearcat.Hosters.FileQ.Api.ApiClient;
using FileQConfig = Bearcat.Hosters.FileQ.FileQConfig;
using FileQHoster = Bearcat.Hosters.FileQ.FileQ;
using FileServeApiClient = Bearcat.Hosters.FileServe.Api.ApiClient;
using FileServeConfig = Bearcat.Hosters.FileServe.FileServeConfig;
using FileServeHoster = Bearcat.Hosters.FileServe.FileServe;
using HxFileApiClient = Bearcat.Hosters.HxFile.Api.ApiClient;
using HxFileConfig = Bearcat.Hosters.HxFile.HxFileConfig;
using HxFileHoster = Bearcat.Hosters.HxFile.HxFile;
using Mega4UploadApiClient = Bearcat.Hosters.Mega4Upload.Api.ApiClient;
using Mega4UploadConfig = Bearcat.Hosters.Mega4Upload.Mega4UploadConfig;
using Mega4UploadHoster = Bearcat.Hosters.Mega4Upload.Mega4Upload;

namespace Bearcat.Hosters.UnitTest.Shared;

public class XFilesharingStorageQuotaTest
{
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

    [Test]
    public async Task GetStorageQuotaAsync_FileQStorageLeftNumberAndStorageUsedNull_ReturnsLimitedQuotaWithoutTotal()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            """{"msg":"OK","result":{"balance":"0.00000","email":"user@example.com","premium_expire":null,"storage_left":53687091200,"storage_used":null},"status":200}"""
        );
        var hoster = CreateFileQHoster(handler);

        // Act
        var result = await hoster.GetStorageQuotaAsync(
            new FileQConfig { ApiKey = "api-key" },
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(HosterStorageQuota.Limited(freeBytes: 53687091200, totalBytes: null));
        handler.RequestUri!.AbsolutePath.ShouldBe("/api/account/info");
        handler.RequestUri.Query.ShouldBe("?key=api-key");
    }

    [Test]
    public async Task GetStorageQuotaAsync_Mega4UploadStorageLeftStringAndStorageUsedNumber_ReturnsLimitedQuotaWithTotal()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            """{"msg":"OK","result":{"balance":"0.00000","email":"user@example.com","premium_expire":null,"storage_left":"189424837716","storage_used":6658874284},"server_time":"2026-10-13 20:02:36","status":200}"""
        );
        var hoster = new Mega4UploadHoster(
            new Mega4UploadApiClient(
                RestService.For<IMega4UploadApi>(CreateHttpClient(handler), RefitSettings),
                CreateHttpClientProvider(),
                new HosterFileDownloader(CreateHttpClientProvider())
            ),
            NullLogger<Mega4UploadHoster>.Instance
        );

        // Act
        var result = await hoster.GetStorageQuotaAsync(
            new Mega4UploadConfig { ApiKey = "api-key" },
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            HosterStorageQuota.Limited(
                freeBytes: 189424837716,
                totalBytes: 189424837716 + 6658874284
            )
        );
    }

    [Test]
    public async Task GetStorageQuotaAsync_FileServeStorageLeftNumberAndStorageUsedString_ReturnsLimitedQuotaWithTotal()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            """{"msg":"OK","result":{"balance":"0.00000","email":"user@example.com","premium_expire":null,"storage_left":530209940375,"storage_used":"6660971625"},"status":200}"""
        );
        var hoster = new FileServeHoster(
            new FileServeApiClient(
                RestService.For<IFileServeApi>(CreateHttpClient(handler), RefitSettings),
                CreateHttpClientProvider(),
                new HosterFileDownloader(CreateHttpClientProvider())
            ),
            NullLogger<FileServeHoster>.Instance
        );

        // Act
        var result = await hoster.GetStorageQuotaAsync(
            new FileServeConfig { ApiKey = "api-key" },
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            HosterStorageQuota.Limited(
                freeBytes: 530209940375,
                totalBytes: 530209940375 + 6660971625
            )
        );
    }

    [Test]
    public async Task GetStorageQuotaAsync_HxFileStorageLeftInf_ReturnsUnlimitedQuota()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            """{"result":{"storage_used":null,"storage_left":"inf","balance":"0.00000","email":"user@example.com","premium_expire":"2026-06-22 18:34:07"},"server_time":"2026-10-13 20:06:16","status":200,"msg":"OK"}"""
        );
        var hoster = new HxFileHoster(
            new HxFileApiClient(
                RestService.For<IHxFileApi>(CreateHttpClient(handler), RefitSettings),
                CreateHttpClientProvider(),
                new HosterFileDownloader(CreateHttpClientProvider())
            ),
            NullLogger<HxFileHoster>.Instance
        );

        // Act
        var result = await hoster.GetStorageQuotaAsync(
            new HxFileConfig { ApiKey = "api-key" },
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(HosterStorageQuota.Unlimited());
    }

    [Test]
    public async Task GetStorageQuotaAsync_ApiRejectsKey_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var handler = new FixedResponseHandler("""{"msg":"Invalid key","status":400}""");
        var hoster = CreateFileQHoster(handler);

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            hoster.GetStorageQuotaAsync(
                new FileQConfig { ApiKey = "api-key" },
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Invalid key");
    }

    [Test]
    public async Task GetStorageQuotaAsync_ApiReturnsErrorStatus_ThrowsWithApiMessage()
    {
        // Arrange
        var handler = new FixedResponseHandler("""{"msg":"Internal error","status":500}""");
        var hoster = CreateFileQHoster(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            hoster.GetStorageQuotaAsync(
                new FileQConfig { ApiKey = "api-key" },
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("fileq.net account info request failed: Internal error");
    }

    private static FileQHoster CreateFileQHoster(HttpMessageHandler handler)
    {
        return new FileQHoster(
            new FileQApiClient(
                RestService.For<IFileQApi>(CreateHttpClient(handler), RefitSettings),
                CreateHttpClientProvider(),
                new HosterFileDownloader(CreateHttpClientProvider())
            ),
            NullLogger<FileQHoster>.Instance
        );
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler) { BaseAddress = new Uri("https://xfilesharing.test/api") };
    }

    private static HttpClientProvider CreateHttpClientProvider()
    {
        return new HttpClientProvider(new Mock<IHttpClientFactory>().Object);
    }

    private sealed class FixedResponseHandler(string responseContent) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseContent),
                }
            );
        }
    }
}
