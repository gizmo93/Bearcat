using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.FileCrypt;
using Bearcat.LinkCrypters.FileCrypt.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.LinkCrypters.UnitTest.FileCrypt;

public class FileCryptTest
{
    private Mock<IFileCryptApi> apiMock = null!;
    private LinkCrypters.FileCrypt.FileCrypt service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IFileCryptApi>(MockBehavior.Strict);
        service = new LinkCrypters.FileCrypt.FileCrypt(apiMock.Object);
    }

    [Test]
    public void SupportedSettings_FileCryptApiSettings_ReturnsDocumentedOptions()
    {
        service.SupportsCaptcha.ShouldBeTrue();
        service.SupportsContainerDownload.ShouldBeTrue();
        service.SupportsClickAndLoad.ShouldBeTrue();
    }

    [Test]
    public async Task CreateContainerAsync_ApiCreatesContainer_ReturnsContainerLinkAndExternalReference()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Response
                {
                    State = 1,
                    Container =
                    [
                        new ContainerResponse
                        {
                            Link = "https://filecrypt.cc/Container/60598C0844.html",
                            Name = "container-name",
                            StatusImgId = "87C8C585C1",
                        },
                    ],
                }
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
        result.ContainerLink.ShouldBe("https://filecrypt.cc/Container/60598C0844.html");
        result.ExternalReference.ShouldNotBeNull();
        result.ExternalReference!.ShouldContain("\"ContainerId\":\"60598C0844\"");
        result.ExternalReference.ShouldContain("\"Name\":\"container-name\"");
        result.StatusImageId.ShouldBe("87C8C585C1");
        result.ErrorMessages.ShouldBeEmpty();

        apiMock.Verify(x =>
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "api_key", "api-key")
                    && HasFormValue(content, "fn", "containerV2")
                    && HasFormValue(content, "sub", "createV2")
                    && HasFormValue(content, "name", "container-name")
                    && HasFormValue(content, "password", "password")
                    && HasFormValue(content, "captcha", "1")
                    && HasFormValue(content, "allow_cnl", "1")
                    && HasFormValue(content, "allow_dlc", "1")
                    && HasFormValue(content, "allow_links", "1")
                    && HasFormValue(content, "mirror_1[0][0]", "https://hoster.test/file-1")
                    && HasFormValue(content, "mirror_1[0][1]", "https://hoster.test/file-2")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_DisabledSettings_SendsDisabledFileCryptOptions()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Response
                {
                    State = 1,
                    Container =
                    [
                        new ContainerResponse
                        {
                            Link = "https://filecrypt.cc/Container/60598C0844.html",
                            Name = "container-name",
                        },
                    ],
                }
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
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "captcha", "0")
                    && HasFormValue(content, "allow_cnl", "0")
                    && HasFormValue(content, "allow_dlc", "0")
                    && HasFormValue(content, "allow_links", "1")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task CreateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 0, Error = "invalid api key" });

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
    }

    [Test]
    public async Task UpdateContainerAsync_ApiUpdatesContainer_ReturnsSuccess()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };
        var externalReference = """{"ContainerId":"60598C0844","Name":"container-name"}""";

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Response
                {
                    State = 1,
                    Container =
                    [
                        new ContainerResponse
                        {
                            Link = "https://filecrypt.cc/Container/60598C0844.html",
                            Name = "container-name",
                            StatusImgId = "87C8C585C1",
                        },
                    ],
                }
            );

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://filecrypt.cc/Container/60598C0844.html",
            externalReference,
            "password",
            links,
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);
        result.ErrorMessage.ShouldBeNull();
        result.StatusImageId.ShouldBe("87C8C585C1");

        apiMock.Verify(x =>
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "api_key", "api-key")
                    && HasFormValue(content, "fn", "containerV2")
                    && HasFormValue(content, "sub", "editV2")
                    && HasFormValue(content, "container_id", "60598C0844")
                    && HasFormValue(content, "name", "container-name")
                    && HasFormValue(content, "password", "password")
                    && HasFormValue(content, "captcha", "1")
                    && HasFormValue(content, "allow_cnl", "1")
                    && HasFormValue(content, "allow_dlc", "1")
                    && HasFormValue(content, "allow_links", "1")
                    && HasFormValue(content, "mirror_1[0][0]", "https://hoster.test/file-1")
                    && HasFormValue(content, "mirror_1[0][1]", "https://hoster.test/file-2")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_DisabledSettings_SendsDisabledFileCryptOptions()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };
        var externalReference = """{"ContainerId":"60598C0844","Name":"container-name"}""";

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 1 });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://filecrypt.cc/Container/60598C0844.html",
            externalReference,
            null,
            ["https://hoster.test/file"],
            enableCaptcha: false,
            enableContainerDownload: false,
            enableClickAndLoad: false,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);

        apiMock.Verify(x =>
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "captcha", "0")
                    && HasFormValue(content, "allow_cnl", "0")
                    && HasFormValue(content, "allow_dlc", "0")
                    && HasFormValue(content, "allow_links", "1")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ExternalReferenceIsPlainId_UsesIdAsName()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 1 });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://filecrypt.cc/Container/60598C0844.html",
            "60598C0844",
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);

        apiMock.Verify(x =>
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "container_id", "60598C0844")
                    && HasFormValue(content, "name", "60598C0844")
                    && !HasFormName(content, "password")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task UpdateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 0, Error = "edit failed" });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://filecrypt.cc/Container/60598C0844.html",
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
        result.Outcome.ShouldBe(UpdateContainerOutcome.Failed);
        result.ErrorMessage.ShouldBe("edit failed");
    }

    [Test]
    public async Task UpdateContainerAsync_ContainerIsNotAvailable_ReturnsContainerNotFound()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 0, Error = "this container is not available" });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://filecrypt.cc/Container/60598C0844.html",
            null,
            null,
            ["https://hoster.test/file"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.ContainerNotFound);
        result.ErrorMessage.ShouldBe("this container is not available");
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsValid_ReturnsSuccess()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 1, Key = "api-key" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();

        apiMock.Verify(x =>
            x.SendAsync(
                It.Is<FormUrlEncodedContent>(content =>
                    HasFormValue(content, "api_key", "api-key")
                    && HasFormValue(content, "fn", "user")
                    && HasFormValue(content, "sub", "apikey")
                ),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Response { State = 0, Error = "invalid api key" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid api key");
    }

    [Test]
    public void DeserializeConfig_SerializedConfig_ReturnsFileCryptConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new FileCryptConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<FileCryptConfig>().ApiKey.ShouldBe("api-key");
    }

    [Test]
    public async Task SendAsync_RefitSendsFormUrlEncodedBody()
    {
        // Arrange
        using var httpMessageHandler = new TestHttpMessageHandler();
        var capturedBody = string.Empty;
        var capturedContentType = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe("https://www.filecrypt.cc/api.php");
            request.Content.ShouldNotBeNull();

            capturedContentType = request.Content!.Headers.ContentType!.ToString();
            capturedBody = await request.Content.ReadAsStringAsync();

            return CreateJsonResponse(
                """
                {"container":{"link":"https://filecrypt.cc/Container/60598C0844.html","name":"container-name"},"state":1}
                """
            );
        });

        var api = RestService.For<IFileCryptApi>(
            new HttpClient(httpMessageHandler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://www.filecrypt.cc"),
            }
        );

        using var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("api_key", "api-key"),
            new KeyValuePair<string, string>("fn", "containerV2"),
            new KeyValuePair<string, string>("sub", "createV2"),
            new KeyValuePair<string, string>("mirror_1[0][0]", "https://hoster.test/file"),
        ]);

        // Act
        var response = await api.SendAsync(content, CancellationToken.None);

        // Assert
        response.State.ShouldBe(1);
        response.Container![0].Link.ShouldBe("https://filecrypt.cc/Container/60598C0844.html");
        capturedContentType.ShouldBe("application/x-www-form-urlencoded");
        capturedBody.ShouldContain("api_key=api-key");
        capturedBody.ShouldContain("fn=containerV2");
        capturedBody.ShouldContain("sub=createV2");
        capturedBody.ShouldContain("mirror_1%5B0%5D%5B0%5D=https%3A%2F%2Fhoster.test%2Ffile");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task ListContainersAsync_ListV3ReturnsKeyedContainers_ReturnsContainersWithExternalReference()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };
        var response = JsonSerializer.Deserialize<Response>(
            """
            {"state":1,"container":{
              "_5B4FB55E52":{"id":"5B4FB55E52","link":"https://www.filecrypt.cc/Container/5B4FB55E52.html","name":"First.Release","tags":["p1080"],"hoster":["1fichier.com"],"size":123,"links":14,"status":1,"statusimg_id":"AAAAAAAAAA","created":1780256887,"edited":0,"views":{"today":0,"week":0,"all":1},"group":0},
              "_C938EFD724":{"id":"C938EFD724","link":"https://www.filecrypt.cc/Container/C938EFD724.html","name":"Second.Release","tags":[],"hoster":["turbobit.net"],"size":456,"links":"11","status":1,"statusimg_id":"BBBBBBBBBB","created":1780256888,"edited":0,"views":{"today":0,"week":0,"all":1},"group":0}
            }}
            """
        )!;

        apiMock
            .Setup(x =>
                x.SendAsync(It.IsAny<FormUrlEncodedContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(response);

        // Act
        var result = await service.ListContainersAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Containers.Count.ShouldBe(2);
        result.Containers[0].ContainerId.ShouldBe("5B4FB55E52");
        result
            .Containers[0]
            .ContainerUrl.ShouldBe("https://www.filecrypt.cc/Container/5B4FB55E52.html");
        result.Containers[0].ContainerName.ShouldBe("First.Release");
        result
            .Containers[0]
            .ExternalReference.ShouldBe("""{"ContainerId":"5B4FB55E52","Name":"First.Release"}""");
        result.Containers[1].ContainerId.ShouldBe("C938EFD724");

        apiMock.Verify(
            x =>
                x.SendAsync(
                    It.Is<FormUrlEncodedContent>(content =>
                        HasFormValue(content, "sub", "listV3")
                        && HasFormValue(content, "offset", "0")
                        && HasFormValue(content, "limit", "500")
                        && HasFormValue(content, "sort", "created")
                        && HasFormValue(content, "sort_mode", "ASC")
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ReadContainerLinksAsync_InfoReturnsMirrorsWithBackup_ReturnsLinksPerMirrorInMirrorOrder()
    {
        // Arrange
        var config = new FileCryptConfig { ApiKey = "api-key" };
        var response = JsonSerializer.Deserialize<ContainerInfoResponse>(
            """
            {"container":{
              "mirror_2":{"links":["https://rapidgator.net/file/second"],"backup":[]},
              "mirror_1":{"links":["https://trbt.cc/first1.html","https://trbt.cc/first2.html"],"backup":[["https://trbt.cc/backup.html"]]}
            }}
            """
        )!;

        apiMock
            .Setup(x =>
                x.GetContainerInfoAsync(
                    It.Is<FormUrlEncodedContent>(content =>
                        HasFormValue(content, "sub", "info")
                        && HasFormValue(content, "container_id", "C938EFD724")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        // Act
        var result = await service.ReadContainerLinksAsync(
            linkCrypterConfig: config,
            containerUrl: "https://www.filecrypt.cc/Container/C938EFD724.html",
            externalReference: """{"ContainerId":"C938EFD724","Name":"Second.Release"}""",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.LinksPerMirror.Count.ShouldBe(2);
        result
            .LinksPerMirror[0]
            .ShouldBe(["https://trbt.cc/first1.html", "https://trbt.cc/first2.html"]);
        result.LinksPerMirror[1].ShouldBe(["https://rapidgator.net/file/second"]);
        result.HasBackupLinks.ShouldBeTrue();
    }

    private static bool HasFormValue(FormUrlEncodedContent content, string name, string value)
    {
        return FormValues(content)[name] == value;
    }

    private static bool HasFormName(FormUrlEncodedContent content, string name)
    {
        return FormValues(content).ContainsKey(name);
    }

    private static IReadOnlyDictionary<string, string> FormValues(FormUrlEncodedContent content)
    {
        return content
            .ReadAsStringAsync()
            .GetAwaiter()
            .GetResult()
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => WebUtility.UrlDecode(part[0]),
                part => part.Length > 1 ? WebUtility.UrlDecode(part[1]) : string.Empty
            );
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
            return responses.Dequeue().Invoke(request);
        }
    }
}
