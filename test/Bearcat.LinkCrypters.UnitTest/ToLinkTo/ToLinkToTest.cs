using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.LinkCrypters.ToLinkTo;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Moq;
using Refit;
using Shouldly;
using CreateFolderRequestBody = Bearcat.LinkCrypters.ToLinkTo.Api.CreateFolder.RequestBody;
using EditFolderRequestBody = Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder.RequestBody;
using EditFolderResponseBody = Bearcat.LinkCrypters.ToLinkTo.Api.EditFolder.ResponseBody;
using PingRequestBody = Bearcat.LinkCrypters.ToLinkTo.Api.Ping.RequestBody;

namespace Bearcat.LinkCrypters.UnitTest.ToLinkTo;

public class ToLinkToTest
{
    private Mock<IToLinkToApi> apiMock = null!;
    private LinkCrypters.ToLinkTo.ToLinkTo service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IToLinkToApi>(MockBehavior.Strict);
        service = new LinkCrypters.ToLinkTo.ToLinkTo(apiMock.Object);
    }

    [Test]
    public void SupportedSettings_ToLinkToApiSettings_ReturnsDocumentedOptions()
    {
        service.SupportsCaptcha.ShouldBeTrue();
        service.SupportsContainerDownload.ShouldBeTrue();
        service.SupportsClickAndLoad.ShouldBeTrue();
    }

    [Test]
    public async Task CreateContainerAsync_ApiCreatesFolder_ReturnsContainerLink()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    It.IsAny<ApiRequest<CreateFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSuccessResponse("https://tolink.to/f/fo587f92cc4c213"));

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
        result.ContainerLink.ShouldBe("https://tolink.to/f/fo587f92cc4c213");
        result.ExternalReference.ShouldBe("fo587f92cc4c213");
        result.ErrorMessages.ShouldBeEmpty();

        apiMock.Verify(x =>
            x.CreateFolderAsync(
                It.Is<ApiRequest<CreateFolderRequestBody>>(request =>
                    request.ApiKey == "api-key"
                    && request.Body.Title == "container-name"
                    && request.Body.Links == "https://hoster.test/file-1;https://hoster.test/file-2"
                    && request.Body.Options.Web
                    && request.Body.Options.Container
                    && request.Body.Options.ClickAndLoad
                    && request.Body.Options.Captcha
                    && !request.Body.Options.CaptchaText
                    && request.Body.Options.Password == "password"
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_DisabledSettings_SendsDisabledFolderOptions()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    It.IsAny<ApiRequest<CreateFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSuccessResponse("https://tolink.to/f/fo587f92cc4c213"));

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
            x.CreateFolderAsync(
                It.Is<ApiRequest<CreateFolderRequestBody>>(request =>
                    request.Body.Options.Web
                    && !request.Body.Options.Container
                    && !request.Body.Options.ClickAndLoad
                    && !request.Body.Options.Captcha
                    && !request.Body.Options.CaptchaText
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    It.IsAny<ApiRequest<CreateFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateErrorResponse<string>("invalid api key", errorCode: 401));

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
            x.CreateFolderAsync(
                It.Is<ApiRequest<CreateFolderRequestBody>>(request =>
                    request.ApiKey == "api-key"
                    && request.Body.Title == "container-name"
                    && request.Body.Links == "https://hoster.test/file"
                    && request.Body.Options.Password == string.Empty
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ApiEditsFolder_ReturnsSuccess()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.EditFolderAsync(
                    It.IsAny<ApiRequest<EditFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateSuccessResponse(
                    new EditFolderResponseBody { Affected = 1, Folder = "folder-id" }
                )
            );

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://tolink.to/f/fo587f92cc4c213",
            "folder-id",
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
            x.EditFolderAsync(
                It.Is<ApiRequest<EditFolderRequestBody>>(request =>
                    request.ApiKey == "api-key"
                    && request.Body.Folder == "folder-id"
                    && request.Body.Title == "folder-id"
                    && request.Body.Links == "https://hoster.test/file-1;https://hoster.test/file-2"
                    && request.Body.Options.Container
                    && request.Body.Options.ClickAndLoad
                    && request.Body.Options.Captcha
                    && !request.Body.Options.CaptchaText
                    && request.Body.Options.Password == "password"
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_DisabledSettings_SendsDisabledFolderOptions()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.EditFolderAsync(
                    It.IsAny<ApiRequest<EditFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateSuccessResponse(
                    new EditFolderResponseBody { Affected = 1, Folder = "folder-id" }
                )
            );

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://tolink.to/f/fo587f92cc4c213",
            "folder-id",
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
            x.EditFolderAsync(
                It.Is<ApiRequest<EditFolderRequestBody>>(request =>
                    request.Body.Options.Web
                    && !request.Body.Options.Container
                    && !request.Body.Options.ClickAndLoad
                    && !request.Body.Options.Captcha
                    && !request.Body.Options.CaptchaText
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ExternalReferenceIsMissing_UsesFolderAliasFromLink()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.EditFolderAsync(
                    It.IsAny<ApiRequest<EditFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateSuccessResponse(
                    new EditFolderResponseBody { Affected = 1, Folder = "fo587f92cc4c213" }
                )
            );

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://tolink.to/f/fo587f92cc4c213",
            null,
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();

        apiMock.Verify(x =>
            x.EditFolderAsync(
                It.Is<ApiRequest<EditFolderRequestBody>>(request =>
                    request.Body.Folder == "fo587f92cc4c213"
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.EditFolderAsync(
                    It.IsAny<ApiRequest<EditFolderRequestBody>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateErrorResponse<EditFolderResponseBody>("folder not found"));

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://tolink.to/f/folder-id",
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
        result.ErrorMessage.ShouldBe("folder not found");
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsValid_ReturnsSuccess()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.PingAsync(It.IsAny<ApiRequest<PingRequestBody>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateSuccessResponse("Pong"));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        apiMock.Verify(x =>
            x.PingAsync(
                It.Is<ApiRequest<PingRequestBody>>(request =>
                    request.ApiKey == "api-key" && request.Body.Message == "Ping"
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new ToLinkToConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.PingAsync(It.IsAny<ApiRequest<PingRequestBody>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateErrorResponse<string>("invalid api key"));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid api key");
    }

    [Test]
    public void DeserializeConfig_SerializedConfig_ReturnsToLinkToConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new ToLinkToConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ToLinkToConfig>().ApiKey.ShouldBe("api-key");
    }

    [Test]
    public async Task CreateFolderAsync_RefitSendsJsonBody()
    {
        // Arrange
        using var httpMessageHandler = new TestHttpMessageHandler();
        var capturedBody = string.Empty;
        var capturedContentType = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe("https://tolink.to/api/v1/folder/create");
            request.Content.ShouldNotBeNull();

            capturedContentType = request.Content!.Headers.ContentType!.ToString();
            capturedBody = await request.Content.ReadAsStringAsync();

            return CreateJsonResponse(
                """
                {"response":{"status":"OK","errorCode":0,"errorMsg":"","body":"https://tolink.to/f/folder-id"}}
                """
            );
        });

        var api = RestService.For<IToLinkToApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://tolink.to"),
            }
        );

        var request = new ApiRequest<CreateFolderRequestBody>
        {
            ApiKey = "api-key",
            Body = new CreateFolderRequestBody
            {
                Title = "container-name",
                Links = "https://hoster.test/file-1;https://hoster.test/file-2",
                Options = new FolderOptions
                {
                    Web = true,
                    Container = false,
                    ClickAndLoad = true,
                    Captcha = false,
                    CaptchaText = false,
                    Password = "password",
                },
            },
        };

        // Act
        var response = await api.CreateFolderAsync(request, CancellationToken.None);

        // Assert
        response.Response.Body.ShouldBe("https://tolink.to/f/folder-id");
        capturedContentType.ShouldBe("application/json; charset=utf-8");
        capturedBody.ShouldContain("\"apikey\":\"api-key\"");
        capturedBody.ShouldContain(
            "\"links\":\"https://hoster.test/file-1;https://hoster.test/file-2\""
        );
        capturedBody.ShouldContain("\"container\":false");
        capturedBody.ShouldContain("\"cln\":true");
        capturedBody.ShouldContain("\"captcha\":false");
        capturedBody.ShouldContain("\"captcha_text\":false");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    private static LinkCrypters.ToLinkTo.Api.ApiResponse<TBody> CreateSuccessResponse<TBody>(
        TBody body
    )
    {
        return new LinkCrypters.ToLinkTo.Api.ApiResponse<TBody>
        {
            Response = new ApiResponseContent<TBody>
            {
                Status = "OK",
                ErrorCode = 0,
                ErrorMessage = string.Empty,
                Body = body,
            },
        };
    }

    private static LinkCrypters.ToLinkTo.Api.ApiResponse<TBody> CreateErrorResponse<TBody>(
        string errorMessage,
        int errorCode = 1
    )
    {
        return new LinkCrypters.ToLinkTo.Api.ApiResponse<TBody>
        {
            Response = new ApiResponseContent<TBody>
            {
                Status = "ERROR",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            },
        };
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
