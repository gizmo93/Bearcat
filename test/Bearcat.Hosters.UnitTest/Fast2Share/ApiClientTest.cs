using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Fast2Share;
using Bearcat.Hosters.Fast2Share.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fast2Share;

public class ApiClientTest
{
    private const string Uuid = "9jUMitPVq3AN";

    private Mock<IFast2ShareApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private ApiClient apiClient = null!;
    private Fast2ShareConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiMock = new Mock<IFast2ShareApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            StatusPollInterval = TimeSpan.Zero,
        };

        config = new Fast2ShareConfig { ApiKey = "f2s_api-key" };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "fast2share test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_FileAlreadyInAccount_ReturnsExistingLinkWithoutUploading()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.CreateUploadAsync(
                    "Bearer f2s_api-key",
                    It.IsAny<CreateUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorApiResponseAsync<CreateUploadResponse>(
                    HttpStatusCode.Conflict,
                    $$"""
                    {"status":"exists","uuid":"{{Uuid}}","share_url":"https://f2s.im/f/{{Uuid}}"}
                    """
                )
            );

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.Deduped.ShouldBeTrue();
        result.Uuid.ShouldBe(Uuid);
        result.FileUrl.ShouldBe($"https://f2s.im/f/{Uuid}");
    }

    [Test]
    public async Task UploadFileAsync_DedupedByFingerprint_ReturnsShareUrlWithoutUploading()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.CreateUploadAsync(
                    "Bearer f2s_api-key",
                    It.IsAny<CreateUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new CreateUploadResponse(
                        Uuid: Uuid,
                        Status: "completed",
                        Deduped: true,
                        Size: 23,
                        Sha256: "9f86d0",
                        ShareUrl: $"https://f2s.im/f/{Uuid}",
                        UploadUrl: null,
                        UploadToken: null,
                        Protocol: null,
                        MetadataKey: null,
                        Error: null
                    )
                )
            );

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.Deduped.ShouldBeTrue();
        result.FileUrl.ShouldBe($"https://f2s.im/f/{Uuid}");
    }

    [Test]
    public async Task UploadFileAsync_NeedHash_ConfirmsWithFullFileSha256()
    {
        // Arrange
        var expectedSha256 = Convert.ToHexStringLower(
            SHA256.HashData(await File.ReadAllBytesAsync(filePath))
        );

        apiMock
            .Setup(x =>
                x.CreateUploadAsync(
                    "Bearer f2s_api-key",
                    It.IsAny<CreateUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    CreateTicket(Uuid, "need_hash") with
                    {
                        UploadUrl = null,
                        UploadToken = null,
                    }
                )
            );

        apiMock
            .Setup(x =>
                x.ConfirmUploadAsync(
                    "Bearer f2s_api-key",
                    Uuid,
                    It.Is<ConfirmUploadRequest>(request => request.Sha256 == expectedSha256),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new CreateUploadResponse(
                        Uuid: Uuid,
                        Status: "completed",
                        Deduped: true,
                        Size: 23,
                        Sha256: expectedSha256,
                        ShareUrl: $"https://f2s.im/f/{Uuid}",
                        UploadUrl: null,
                        UploadToken: null,
                        Protocol: null,
                        MetadataKey: null,
                        Error: null
                    )
                )
            );

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.Deduped.ShouldBeTrue();
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadFileAsync_NewFile_RunsTusUploadAndMovesFileToFolder()
    {
        // Arrange
        var httpMessageHandler = new TusTestHttpMessageHandler();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler));

        apiMock
            .Setup(x =>
                x.CreateUploadAsync(
                    "Bearer f2s_api-key",
                    It.IsAny<CreateUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse(HttpStatusCode.OK, CreateTicket(Uuid, "pending")));

        apiMock
            .Setup(x =>
                x.GetFileStatusAsync("Bearer f2s_api-key", Uuid, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new FileStatusResponse(
                        Uuid: Uuid,
                        Status: "completed",
                        Size: 23,
                        ShareUrl: $"https://f2s.im/f/{Uuid}"
                    )
                )
            );

        apiMock
            .Setup(x =>
                x.MoveFileAsync(
                    "Bearer f2s_api-key",
                    Uuid,
                    It.Is<MoveFileRequest>(request => request.FolderId == 12),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new MoveFileResponse(Uuid, "completed", null, 12)
                )
            );

        // Act
        var result = await UploadAsync(folderId: "12");

        // Assert
        result.Deduped.ShouldBeFalse();
        result.FileUrl.ShouldBe($"https://f2s.im/f/{Uuid}");
        httpMessageHandler.CreationMetadata.ShouldBe(
            $"token {Convert.ToBase64String(Encoding.UTF8.GetBytes("upload-token"))}"
        );
        httpMessageHandler.UploadedBytes.ShouldBe(new FileInfo(filePath).Length);
        apiMock.VerifyAll();
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolder_ReturnsExistingIdWithoutCreating()
    {
        // Arrange
        apiMock
            .Setup(x => x.ListFoldersAsync("Bearer f2s_api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new FolderListResponse([
                        new FolderResponse(19, "Release.Name", ParentId: 12),
                        new FolderResponse(12, "release.name", ParentId: null),
                    ])
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "Release.Name",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("12");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<CreateFolderRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_NameTakenWhileCreating_ReturnsIdFromSecondLookup()
    {
        // Arrange
        apiMock
            .SetupSequence(x =>
                x.ListFoldersAsync("Bearer f2s_api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse(HttpStatusCode.OK, new FolderListResponse([])))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new FolderListResponse([new FolderResponse(42, "Release.Name", null)])
                )
            );

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "Bearer f2s_api-key",
                    It.Is<CreateFolderRequest>(request => request.Name == "Release.Name"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorApiResponseAsync<FolderResponse>(
                    HttpStatusCode.Conflict,
                    """{"error":"A folder with this name already exists."}"""
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "Release.Name",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("42");
    }

    [Test]
    public async Task CheckLinksAsync_FileNotFound_MarksLinkOffline()
    {
        // Arrange
        SetupValidApiKey();
        apiMock
            .Setup(x => x.GetFileAsync("Bearer f2s_api-key", Uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        var fileUrl = $"https://f2s.im/f/{Uuid}";

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
    }

    [Test]
    public async Task CheckLinksAsync_FileFound_MapsOnlineStatusAndDownloadCount()
    {
        // Arrange
        SetupValidApiKey();
        apiMock
            .Setup(x => x.GetFileAsync("Bearer f2s_api-key", Uuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new FileResponse(
                        Uuid: Uuid,
                        Name: "archive.rar",
                        Size: 184320,
                        Status: "completed",
                        Downloads: 23,
                        ShareUrl: $"https://f2s.im/f/{Uuid}"
                    )
                )
            );

        var fileUrl = $"https://f2s.im/f/{Uuid}";

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeTrue();
        result[fileUrl].DownloadCount.ShouldBe(23);
    }

    [Test]
    public async Task CheckLinksAsync_LinkCheckThrows_OmitsLinkInsteadOfMarkingOffline()
    {
        // Arrange
        SetupValidApiKey();
        apiMock
            .Setup(x => x.GetFileAsync("Bearer f2s_api-key", Uuid, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var fileUrl = $"https://f2s.im/f/{Uuid}";

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public void ExtractUuid_ShareUrl_ReturnsUuid()
    {
        // Arrange
        var fileUrl = $"https://f2s.im/f/{Uuid}";

        // Act
        var result = ApiClient.ExtractUuid(fileUrl);

        // Assert
        result.ShouldBe(Uuid);
    }

    [Test]
    public void ExtractUuid_LegacyDomainShareUrl_ReturnsUuid()
    {
        // Act
        var result = ApiClient.ExtractUuid($"https://fast2share.com/f/{Uuid}");

        // Assert
        result.ShouldBe(Uuid);
    }

    [Test]
    public void ExtractUuid_UrlWithoutFileSegment_ReturnsNull()
    {
        // Act
        var result = ApiClient.ExtractUuid("https://fast2share.com/checker");

        // Assert
        result.ShouldBeNull();
    }

    private void SetupValidApiKey()
    {
        apiMock
            .Setup(x => x.GetUserAsync("Bearer f2s_api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(HttpStatusCode.OK, new UserResponse(42, "user@example.com", null))
            );
    }

    private async Task<Fast2ShareUploadResult> UploadAsync(string? folderId)
    {
        var fileInfo = new FileInfo(filePath);
        await using var stream = File.OpenRead(filePath);

        return await apiClient.UploadFileAsync(
            config: config,
            fullFileName: filePath,
            stream: stream,
            fileName: fileInfo.Name,
            fileSize: fileInfo.Length,
            folderId: folderId,
            cancellationToken: CancellationToken.None
        );
    }

    private static CreateUploadResponse CreateTicket(string uuid, string status)
    {
        return new CreateUploadResponse(
            Uuid: uuid,
            Status: status,
            Deduped: null,
            Size: null,
            Sha256: null,
            ShareUrl: null,
            UploadUrl: "https://storage1.fast2share.com/files/",
            UploadToken: "upload-token",
            Protocol: "tus",
            MetadataKey: "token",
            Error: null
        );
    }

    private static ApiResponse<T> CreateApiResponse<T>(
        HttpStatusCode statusCode,
        T? content = default
    )
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content!,
            new RefitSettings(),
            error: null
        );
    }

    private static async Task<ApiResponse<T>> CreateErrorApiResponseAsync<T>(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.fast2share.com/v1/test");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var settings = new RefitSettings();
        var error = await ApiException.Create(request, HttpMethod.Post, response, settings);

        return new ApiResponse<T>(response, default!, settings, error);
    }

    private sealed class TusTestHttpMessageHandler : HttpMessageHandler
    {
        public string? CreationMetadata { get; private set; }

        public long UploadedBytes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.Method == HttpMethod.Post)
            {
                CreationMetadata = request.Headers.GetValues("Upload-Metadata").First();

                var creationResponse = new HttpResponseMessage(HttpStatusCode.Created);
                creationResponse.Headers.Location = new Uri(
                    "https://storage1.fast2share.com/files/upload-id"
                );

                return creationResponse;
            }

            var chunk = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            UploadedBytes += chunk.Length;

            var patchResponse = new HttpResponseMessage(HttpStatusCode.NoContent);
            patchResponse.Headers.Add("Upload-Offset", UploadedBytes.ToString());

            return patchResponse;
        }
    }
}
