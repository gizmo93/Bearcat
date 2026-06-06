using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.LinkCrypters.KeepLinks;
using Bearcat.LinkCrypters.KeepLinks.Api;
using Moq;
using Refit;
using Shouldly;
using ProtectLinksResponse = Bearcat.LinkCrypters.KeepLinks.Api.ProtectLinks.Response;

namespace Bearcat.LinkCrypters.UnitTest.KeepLinks;

public class KeepLinksTest
{
    private Mock<IKeepLinksApi> apiMock = null!;
    private LinkCrypters.KeepLinks.KeepLinks service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKeepLinksApi>(MockBehavior.Strict);
        service = new LinkCrypters.KeepLinks.KeepLinks(apiMock.Object);
    }

    [Test]
    public void SupportedSettings_KeepLinksApiSettings_ReturnsDocumentedOptions()
    {
        service.SupportsCaptcha.ShouldBeTrue();
        service.SupportsContainerDownload.ShouldBeTrue();
        service.SupportsClickAndLoad.ShouldBeFalse();
    }

    [Test]
    public async Task CreateContainerAsync_ApiProtectsLinks_ReturnsContainerLink()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.ProtectLinkAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProtectLinksResponse { ContainerLink = "https://keeplinks.org/p/abc" }
            );

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            "password",
            links,
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ContainerLink.ShouldBe("https://keeplinks.org/p/abc");
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBeEmpty();

        apiMock.Verify(x =>
            x.ProtectLinkAsync(
                It.Is<MultipartFormDataContent>(content =>
                    HasFormValue(content, "apihash", "api-key")
                    && HasFormValue(content, "output", "json")
                    && HasFormValue(content, "password", "password")
                    && HasFormValue(content, "title", "container-name")
                    && HasFormValue(content, "captcha", "on")
                    && HasFormValue(content, "captchatype", "Re")
                    && HasFormValue(content, "dlc", "on")
                    && FormValues(content, "link-to-protect")
                        .SequenceEqual(
                            new[] { "https://hoster.test/file-1,https://hoster.test/file-2" }
                        )
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_DisabledSettings_OmitsOptionalKeepLinksFields()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.ProtectLinkAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProtectLinksResponse { ContainerLink = "https://keeplinks.org/p/abc" }
            );

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            null,
            ["https://hoster.test/file"],
            enableCaptcha: false,
            enableContainerDownload: false,
            enableClickAndLoad: false,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        apiMock.Verify(x =>
            x.ProtectLinkAsync(
                It.Is<MultipartFormDataContent>(content =>
                    !HasFormName(content, "captcha")
                    && !HasFormName(content, "captchatype")
                    && !HasFormName(content, "dlc")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.ProtectLinkAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse { ApiError = "invalid api key" });

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ContainerLink.ShouldBeNull();
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBe(["invalid api key"]);

        apiMock.Verify(x =>
            x.ProtectLinkAsync(
                It.Is<MultipartFormDataContent>(content =>
                    HasFormValue(content, "apihash", "api-key")
                    && HasFormValue(content, "title", "container-name")
                    && FormValues(content, "link-to-protect")
                        .SequenceEqual(new[] { "https://hoster.test/file" })
                    && !HasFormName(content, "password")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ApiUpdatesContainer_ReturnsSuccess()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse());

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://keeplinks.org/p/container-id",
            null,
            "password",
            links,
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        apiMock.Verify(x =>
            x.UpdateContainerAsync(
                It.Is<MultipartFormDataContent>(content =>
                    HasFormValue(content, "apihash", "api-key")
                    && HasFormValue(content, "output", "json")
                    && HasFormValue(content, "password", "password")
                    && HasFormValue(content, "url-id", "container-id")
                    && HasFormValue(content, "captcha", "on")
                    && HasFormValue(content, "captchatype", "Re")
                    && HasFormValue(content, "dlc", "on")
                    && FormValues(content, "link-to-protect")
                        .SequenceEqual(
                            new[] { "https://hoster.test/file-1,https://hoster.test/file-2" }
                        )
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_DisabledSettings_OmitsOptionalKeepLinksFields()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse());

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://keeplinks.org/p/container-id",
            null,
            null,
            ["https://hoster.test/file"],
            enableCaptcha: false,
            enableContainerDownload: false,
            enableClickAndLoad: false,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        apiMock.Verify(x =>
            x.UpdateContainerAsync(
                It.Is<MultipartFormDataContent>(content =>
                    !HasFormName(content, "captcha")
                    && !HasFormName(content, "captchatype")
                    && !HasFormName(content, "dlc")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    It.IsAny<MultipartFormDataContent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse { ApiError = "update failed" });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://keeplinks.org/p/container-id",
            null,
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("update failed");

        apiMock.Verify(x =>
            x.UpdateContainerAsync(
                It.Is<MultipartFormDataContent>(content =>
                    HasFormValue(content, "apihash", "api-key")
                    && HasFormValue(content, "url-id", "container-id")
                    && FormValues(content, "link-to-protect")
                        .SequenceEqual(new[] { "https://hoster.test/file" })
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task TryLoginAsync_ApiHashIsValid_ReturnsSuccess()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x => x.GetLinksAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"url_id":"container-id"}""");

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_ApiHashIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x => x.GetLinksAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync("API hash is not valid");

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("API hash is not valid");
    }

    [Test]
    public void DeserializeConfig_SerializedConfig_ReturnsKeepLinksConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new KeepLinksConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KeepLinksConfig>().ApiKey.ShouldBe("api-key");
    }

    [Test]
    public async Task ProtectLinkAsync_RefitSendsMultipartFormBody()
    {
        // Arrange
        using var httpMessageHandler = new TestHttpMessageHandler();
        var capturedBody = string.Empty;
        var capturedContentType = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe("https://www.keeplinks.org/api.php");
            request.Content.ShouldNotBeNull();

            capturedContentType = request.Content!.Headers.ContentType!.ToString();
            capturedBody = await request.Content.ReadAsStringAsync();

            return CreateJsonResponse("""{"p_links":"https://keeplinks.org/p/abc"}""");
        });

        var api = RestService.For<IKeepLinksApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://www.keeplinks.org"),
            }
        );

        using var content = new MultipartFormDataContent();
        AddFormField(content, "apihash", "api-key");
        AddFormField(
            content,
            "link-to-protect",
            "https://hoster.test/file-1,https://hoster.test/file-2"
        );

        // Act
        var response = await api.ProtectLinkAsync(content, CancellationToken.None);

        // Assert
        response.ContainerLink.ShouldBe("https://keeplinks.org/p/abc");
        capturedContentType.ShouldStartWith("multipart/form-data; boundary=");
        capturedBody.Split("name=link-to-protect").Length.ShouldBe(2);
        capturedBody.ShouldContain("https://hoster.test/file-1,https://hoster.test/file-2");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    private static bool HasFormValue(MultipartFormDataContent content, string name, string value)
    {
        return FormValues(content, name).Contains(value);
    }

    private static bool HasFormName(MultipartFormDataContent content, string name)
    {
        return FormValues(content, name).Any();
    }

    private static IEnumerable<string> FormValues(MultipartFormDataContent content, string name)
    {
        return content
            .Where(part => part.Headers.ContentDisposition?.Name?.Trim('"') == name)
            .Select(part => part.ReadAsStringAsync().GetAwaiter().GetResult());
    }

    private static void AddFormField(MultipartFormDataContent content, string name, string value)
    {
        var field = new StringContent(value);
        field.Headers.ContentType = null;

        content.Add(field, name);
    }

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses = [];

        public int PendingRequests => responses.Count;

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
        {
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            return responses.Dequeue()(request);
        }
    }
}
