using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.ToLinkTo;
using Bearcat.LinkCrypters.ToLinkTo.Api;
using Refit;
using Shouldly;

namespace Bearcat.LinkCrypters.UnitTest.ToLinkTo;

public class ToLinkToTest
{
    private static readonly ToLinkToConfig Config = new() { ApiKey = "api-key" };

    private TestHttpMessageHandler httpMessageHandler = null!;
    private LinkCrypters.ToLinkTo.ToLinkTo service = null!;

    [SetUp]
    public void SetUp()
    {
        httpMessageHandler = new TestHttpMessageHandler();

        var api = RestService.For<IToLinkToApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://tolink.to"),
            }
        );

        service = new LinkCrypters.ToLinkTo.ToLinkTo(api);
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
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
        var capturedBody = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe("https://tolink.to/api/v2/folders");
            request.Headers.Authorization!.ToString().ShouldBe("Bearer api-key");
            capturedBody = await request.Content!.ReadAsStringAsync();

            return CreateJsonResponse(
                HttpStatusCode.Created,
                """
                {"url":"https:\/\/tolink.to\/f\/fo587f92cc4c213","folder":"fo587f92cc4c213","title":"container-name"}
                """
            );
        });

        // Act
        var result = await service.CreateContainerAsync(
            Config,
            "container-name",
            "password",
            new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = ["https://hoster.test/file-1", "https://hoster.test/file-2"],
            },
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ContainerLink.ShouldBe("https://tolink.to/f/fo587f92cc4c213");
        result.ExternalReference.ShouldBe("fo587f92cc4c213");
        result.ErrorMessages.ShouldBeEmpty();

        capturedBody.ShouldContain("\"title\":\"container-name\"");
        capturedBody.ShouldContain(
            "\"links\":\"https://hoster.test/file-1;https://hoster.test/file-2\""
        );
        capturedBody.ShouldContain("\"web\":true");
        capturedBody.ShouldContain("\"container\":true");
        capturedBody.ShouldContain("\"cln\":true");
        capturedBody.ShouldContain("\"captcha\":true");
        capturedBody.ShouldContain("\"captcha_text\":false");
        capturedBody.ShouldContain("\"password\":\"password\"");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task CreateContainerAsync_TwoMirrors_SendsLinksOfAllMirrorsInOneList()
    {
        // Arrange
        var capturedBody = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();

            return CreateJsonResponse(
                HttpStatusCode.Created,
                """
                {"url":"https:\/\/tolink.to\/f\/fo587f92cc4c213","folder":"fo587f92cc4c213","title":"container-name"}
                """
            );
        });

        // Act
        var result = await service.CreateContainerAsync(
            Config,
            "container-name",
            null,
            new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = ["https://hoster.test/file-1"],
                [2] = ["https://hoster.test/file-2"],
            },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedBody.ShouldContain(
            "\"links\":\"https://hoster.test/file-1;https://hoster.test/file-2\""
        );
    }

    [Test]
    public async Task CreateContainerAsync_ApiRejectsParameters_ReturnsErrorMessage()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                CreateJsonResponse(
                    HttpStatusCode.UnprocessableEntity,
                    """{"message":"Wrong param given"}"""
                )
            )
        );

        // Act
        var result = await service.CreateContainerAsync(
            Config,
            "container-name",
            null,
            new Dictionary<int, IReadOnlyList<string>> { [1] = ["https://hoster.test/file"] },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ContainerLink.ShouldBeNull();
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBe(["ToLink.to returned 422: Wrong param given"]);
    }

    [Test]
    public async Task CreateContainerAsync_ApiReturnsNonJsonError_ReturnsStatusMessage()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent("<html>Bad gateway</html>"),
                }
            )
        );

        // Act
        var result = await service.CreateContainerAsync(
            Config,
            "container-name",
            null,
            new Dictionary<int, IReadOnlyList<string>> { [1] = ["https://hoster.test/file"] },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessages.ShouldHaveSingleItem().ShouldContain("502");
    }

    [Test]
    public async Task UpdateContainerAsync_ApiEditsFolder_PatchesLinksAndOptionsOnly()
    {
        // Arrange
        var capturedBody = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Patch);
            request.RequestUri!.ToString().ShouldBe("https://tolink.to/api/v2/folders/folder-id");
            request.Headers.Authorization!.ToString().ShouldBe("Bearer api-key");
            capturedBody = await request.Content!.ReadAsStringAsync();

            return CreateJsonResponse(
                HttpStatusCode.OK,
                """{"folder":"folder-id","title":"container-name","affected":1}"""
            );
        });

        // Act
        var result = await service.UpdateContainerAsync(
            Config,
            "https://tolink.to/f/fo587f92cc4c213",
            "folder-id",
            null,
            new Dictionary<int, IReadOnlyList<string>>
            {
                [1] = ["https://hoster.test/file-1", "https://hoster.test/file-2"],
            },
            enableCaptcha: false,
            enableContainerDownload: false,
            enableClickAndLoad: false,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);
        result.ErrorMessage.ShouldBeNull();

        capturedBody.ShouldNotContain("\"title\"");
        capturedBody.ShouldContain(
            "\"links\":\"https://hoster.test/file-1;https://hoster.test/file-2\""
        );
        capturedBody.ShouldContain("\"web\":true");
        capturedBody.ShouldContain("\"container\":false");
        capturedBody.ShouldContain("\"cln\":false");
        capturedBody.ShouldContain("\"captcha\":false");
        capturedBody.ShouldContain("\"password\":\"\"");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task UpdateContainerAsync_ExternalReferenceIsMissing_UsesFolderAliasFromLink()
    {
        // Arrange
        httpMessageHandler.Enqueue(request =>
        {
            request
                .RequestUri!.ToString()
                .ShouldBe("https://tolink.to/api/v2/folders/fo587f92cc4c213");

            return Task.FromResult(
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """{"folder":"fo587f92cc4c213","title":"container-name","affected":1}"""
                )
            );
        });

        // Act
        var result = await service.UpdateContainerAsync(
            Config,
            "https://tolink.to/f/fo587f92cc4c213",
            null,
            null,
            new Dictionary<int, IReadOnlyList<string>> { [1] = ["https://hoster.test/file"] },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task UpdateContainerAsync_FolderNotFound_ReturnsContainerNotFound()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                CreateJsonResponse(HttpStatusCode.NotFound, """{"message":"Resource not found"}""")
            )
        );

        // Act
        var result = await service.UpdateContainerAsync(
            Config,
            "https://tolink.to/f/folder-id",
            null,
            null,
            new Dictionary<int, IReadOnlyList<string>> { [1] = ["https://hoster.test/file"] },
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.ContainerNotFound);
        result.ErrorMessage.ShouldBe("ToLink.to returned 404: Resource not found");
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsValid_ReturnsSuccess()
    {
        // Arrange
        httpMessageHandler.Enqueue(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request.RequestUri!.ToString().ShouldBe("https://tolink.to/api/v2/ping");
            request.Headers.Authorization!.ToString().ShouldBe("Bearer api-key");

            return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, """{"message":"Pong"}"""));
        });

        // Act
        var result = await service.TryLoginAsync(Config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsErrorMessage()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                CreateJsonResponse(HttpStatusCode.Unauthorized, """{"message":"Invalid API Key"}""")
            )
        );

        // Act
        var result = await service.TryLoginAsync(Config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("ToLink.to returned 401: Invalid API Key");
    }

    [Test]
    public async Task ListContainersAsync_TwoPages_ReturnsFoldersOfAllPages()
    {
        // Arrange
        httpMessageHandler.Enqueue(request =>
        {
            request
                .RequestUri!.ToString()
                .ShouldBe("https://tolink.to/api/v2/folders?order=ASC&limit=200&page=1");
            request.Headers.Authorization!.ToString().ShouldBe("Bearer api-key");

            return Task.FromResult(
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """
                    {"order":"ASC","limit":200,"page":1,"pages":2,"total":2,"folders":[
                      {"id":"4","folder":"fofirst","title":"First.Release","status":"1","hits":1,"hoster":["rapidgator"]}
                    ]}
                    """
                )
            );
        });
        httpMessageHandler.Enqueue(request =>
        {
            request
                .RequestUri!.ToString()
                .ShouldBe("https://tolink.to/api/v2/folders?order=ASC&limit=200&page=2");

            return Task.FromResult(
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """
                    {"order":"ASC","limit":200,"page":2,"pages":2,"total":2,"folders":[
                      {"id":"5","folder":"fosecond","title":"Second.Release","status":"3","hits":0,"hoster":[""]}
                    ]}
                    """
                )
            );
        });

        // Act
        var result = await service.ListContainersAsync(Config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result
            .Containers.Select(container => container.ContainerName)
            .ShouldBe(["First.Release", "Second.Release"]);
        result.Containers[1].ContainerUrl.ShouldBe("https://tolink.to/f/fosecond");
        result.Containers[1].ExternalReference.ShouldBe("fosecond");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task ReadContainerLinksAsync_FolderHasLinksOfTwoHosters_ReturnsOneMirrorPerHost()
    {
        // Arrange
        httpMessageHandler.Enqueue(request =>
        {
            request
                .RequestUri!.ToString()
                .ShouldBe("https://tolink.to/api/v2/folders/fofirst/details");
            request.Headers.Authorization!.ToString().ShouldBe("Bearer api-key");

            return Task.FromResult(
                CreateJsonResponse(
                    HttpStatusCode.OK,
                    """
                    {"id":"4","folder":"fofirst","url":"https://tolink.to/f/fofirst","title":"First.Release","status":1,"links":[
                      {"id":"1","url":"https://freedl.ink/a1","title":"a.part1.rar","status":1,"size":1500,"hits":0,"hoster":"","created":"2026-11-01 12:00:00"},
                      {"id":"2","url":"https://rapidgator.net/file/b1","title":"a.part1.rar","status":1,"size":1500,"hits":0,"hoster":"rapidgator","created":"2026-11-01 12:00:00"},
                      {"id":"3","url":"https://www.freedl.ink/a2","title":"a.part2.rar","status":1,"size":1500,"hits":0,"hoster":"","created":"2026-11-01 12:00:00"},
                      {"id":"4","url":"https://rapidgator.net/file/b2","title":"a.part2.rar","status":1,"size":1500,"hits":0,"hoster":"rapidgator","created":"2026-11-01 12:00:00"}
                    ]}
                    """
                )
            );
        });

        // Act
        var result = await service.ReadContainerLinksAsync(
            linkCrypterConfig: Config,
            containerUrl: "https://tolink.to/f/fofirst",
            externalReference: "fofirst",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.LinksPerMirrorNumber.Count.ShouldBe(2);
        result
            .LinksPerMirrorNumber[1]
            .ShouldBe(["https://freedl.ink/a1", "https://www.freedl.ink/a2"]);
        result
            .LinksPerMirrorNumber[2]
            .ShouldBe(["https://rapidgator.net/file/b1", "https://rapidgator.net/file/b2"]);
    }

    [Test]
    public async Task ReadContainerLinksAsync_FolderOfAnotherAccount_ReturnsNotFoundError()
    {
        // Arrange
        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("Resource not found"),
                }
            )
        );

        // Act
        var result = await service.ReadContainerLinksAsync(
            linkCrypterConfig: Config,
            containerUrl: "https://tolink.to/f/foforeign",
            externalReference: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.LinksPerMirrorNumber.ShouldBeEmpty();
        result.ErrorMessages.Count.ShouldBe(1);
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

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string content)
    {
        return new HttpResponseMessage(statusCode)
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            var response = await responses.Dequeue()(request);
            response.RequestMessage = request;
            return response;
        }
    }
}
