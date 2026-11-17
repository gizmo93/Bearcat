using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Alfafile;
using Bearcat.Hosters.Alfafile.Api;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.User;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Alfafile;

public class AlfafileTest
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly List<string> temporaryFiles = [];
    private Mock<IAlfafileApiClient> apiClientMock = null!;
    private Hosters.Alfafile.Alfafile service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IAlfafileApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Alfafile.Alfafile>>();
        service = new Hosters.Alfafile.Alfafile(apiClientMock.Object, loggerMock.Object)
        {
            UploadRetryDelay = TimeSpan.Zero,
            UploadStatusPollDelay = TimeSpan.Zero,
        };
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporaryFile in temporaryFiles.Where(File.Exists))
        {
            File.Delete(temporaryFile);
        }
    }

    [Test]
    public async Task UploadFileAsync_ApiUploadSucceeds_ReturnsDownloadUrl()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117);
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };
        var uploadRequest = CreateUploadResponse(
            uploadId: "upload-id",
            state: UploadStates.Uploading,
            fileUrl: null
        );
        var uploadResponse = CreateUploadResponse(
            uploadId: "upload-id",
            state: UploadStates.Done,
            fileUrl: "http://alfafile.net/file/GH"
        );

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "61d7464d09ca6098f561542ce138214a",
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(uploadRequest);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "http://upload.alfafile.test/ul/upload-id",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(uploadResponse);

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("http://alfafile.net/file/GH");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_RequestUploadFails_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 18, FullFileName: filePath, UploadId: 118);
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };
        var failedRequest = new UploadFileResponse
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Details = "temporary upload error",
        };

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "61d7464d09ca6098f561542ce138214a",
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failedRequest);

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.FileUrl.ShouldBeNull();
        result.ErrorMessages.ShouldBe([
            "temporary upload error",
            "temporary upload error",
            "temporary upload error",
        ]);
        apiClientMock.Verify(
            x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "61d7464d09ca6098f561542ce138214a",
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task UploadFileAsync_CredentialsRejected_ThrowsWithoutRetrying()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 19, FullFileName: filePath, UploadId: 119);
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Unauthorized. Wrong login or password.")
            );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            service.UploadFileAsync(
                fileDto,
                config,
                NullTransferProgress.Instance,
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Unauthorized. Wrong login or password.");
        apiClientMock.Verify(
            x =>
                x.RequestUploadFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task GetStorageQuotaAsync_UserInfoCarriesStorage_ReturnsLimitedQuota()
    {
        // Arrange
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };
        var json = """
            {"response":{"user":{"email":"user@example.test","is_premium":false,"premium_end_time":null,"state":1,"state_label":"Activated","storage":{"total":53687091200,"left":53687091200}}},"status":200,"details":null}
            """;

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Deserialize<InfoResponse>(json, JsonSerializerOptions)!);

        // Act
        var result = await service.GetStorageQuotaAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBe(
            HosterStorageQuota.Limited(freeBytes: 53687091200, totalBytes: 53687091200)
        );
    }

    [Test]
    public async Task GetStorageQuotaAsync_ApiReturnsErrorStatus_ThrowsHttpRequestExceptionWithDetails()
    {
        // Arrange
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InfoResponse
                {
                    Status = (int)HttpStatusCode.Forbidden,
                    Details = "Access denied",
                }
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            service.GetStorageQuotaAsync(config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Access denied");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }

    private static UploadFileResponse CreateUploadResponse(
        string uploadId,
        int state,
        string? fileUrl
    )
    {
        var stateLabel = state == UploadStates.Done ? "Done" : "Uploading";
        var fileJson = fileUrl is null
            ? "[]"
            : $$"""
                {
                    "file_id": "GH",
                    "mode": 1,
                    "mode_label": "Public",
                    "folder_id": null,
                    "name": "test.bin",
                    "hash": "61d7464d09ca6098f561542ce138214a",
                    "size": 14,
                    "url": "{{fileUrl}}",
                    "created": 1426776799
                }
                """;

        return JsonSerializer.Deserialize<UploadFileResponse>(
            $$"""
            {
                "response": {
                    "upload": {
                        "upload_id": "{{uploadId}}",
                        "url": "http://upload.alfafile.test/ul/{{uploadId}}",
                        "file": {{fileJson}},
                        "state": {{state}},
                        "state_label": "{{stateLabel}}"
                    }
                },
                "status": 200,
                "details": null
            }
            """,
            JsonSerializerOptions
        )!;
    }
}
