using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Hosters.Fast2Share;
using Bearcat.Hosters.Fast2Share.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fast2Share;

public class Fast2ShareStorageQuotaTest
{
    private static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new SystemTextJsonContentSerializer(
            jsonSerializerOptions: new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }
        ),
    };

    private static readonly Fast2ShareConfig Config = new() { ApiKey = "f2s_api-key" };

    [Test]
    public async Task GetStorageQuotaAsync_PremiumAccountFromSpec_ReturnsRemainingBytesOfPlanQuota()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.OK,
            """{"id":42,"email":"you@example.com","display_name":"You","plan":{"name":"premium-monthly","max_file_size":5368709120,"storage_quota":107374182400},"usage":{"used_bytes":2147483648,"file_count":12,"remaining_bytes":105226698752},"download_quota":{"limit_bytes":107374182400,"used_bytes":21474836480,"remaining_bytes":85899345920,"unlimited":false,"resets_at":"2026-08-24T00:00:00+00:00"}}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var result = await hoster.GetStorageQuotaAsync(Config, CancellationToken.None);

        // Assert
        result.ShouldBe(
            HosterStorageQuota.Limited(freeBytes: 105226698752, totalBytes: 107374182400)
        );
        handler.RequestUri!.AbsolutePath.ShouldBe("/v1/user");
        handler.Authorization.ShouldBe("Bearer f2s_api-key");
    }

    [Test]
    public async Task GetStorageQuotaAsync_FreeAccountWithUnlimitedDownloadQuota_ReturnsRemainingBytesOfPlanQuota()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.OK,
            """{"id":1658,"email":"user@example.com","display_name":null,"plan":{"name":"Free","max_file_size":2147483648,"storage_quota":107374182400},"usage":{"used_bytes":0,"file_count":0,"remaining_bytes":107374182400},"download_quota":{"limit_bytes":null,"used_bytes":0,"remaining_bytes":null,"unlimited":true,"resets_at":"2026-10-14T09:00:00+09:00"}}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var result = await hoster.GetStorageQuotaAsync(Config, CancellationToken.None);

        // Assert
        result.ShouldBe(
            HosterStorageQuota.Limited(freeBytes: 107374182400, totalBytes: 107374182400)
        );
    }

    [Test]
    public async Task GetStorageQuotaAsync_ApiTokenRevoked_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.Unauthorized,
            """{"error":"Invalid or revoked API token."}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            hoster.GetStorageQuotaAsync(Config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Invalid or revoked API token.");
    }

    [Test]
    public async Task GetStorageQuotaAsync_ServerError_ThrowsHttpRequestExceptionWithErrorMessage()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.InternalServerError,
            """{"error":"Internal error"}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            hoster.GetStorageQuotaAsync(Config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe(
            "Fast2Share user request failed with status code InternalServerError: Internal error"
        );
        exception.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task GetStorageQuotaAsync_RemainingBytesNull_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new FixedResponseHandler(
            HttpStatusCode.OK,
            """{"id":42,"email":"you@example.com","display_name":"You","plan":{"name":"Free","max_file_size":2147483648,"storage_quota":107374182400},"usage":{"used_bytes":0,"file_count":0,"remaining_bytes":null}}"""
        );
        var hoster = CreateHoster(handler);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            hoster.GetStorageQuotaAsync(Config, CancellationToken.None)
        );

        // Assert
        exception.InnerException.ShouldNotBeNull();
    }

    private static Hosters.Fast2Share.Fast2Share CreateHoster(HttpMessageHandler handler)
    {
        var httpClientProvider = new HttpClientProvider(new Mock<IHttpClientFactory>().Object);

        return new Hosters.Fast2Share.Fast2Share(
            new ApiClient(
                RestService.For<IFast2ShareApi>(
                    new HttpClient(handler) { BaseAddress = new Uri(ApiClient.ApiBaseUrl) },
                    RefitSettings
                ),
                httpClientProvider,
                new HosterFileDownloader(httpClientProvider),
                NullLogger<ApiClient>.Instance
            ),
            NullLogger<Hosters.Fast2Share.Fast2Share>.Instance
        );
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode, string responseContent)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();

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
