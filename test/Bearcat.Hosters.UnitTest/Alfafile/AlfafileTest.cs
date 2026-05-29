using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
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
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

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
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failedRequest);

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

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
    public async Task CheckFilesExistAsync_ApiReturnsStatuses_ReturnsStatuses()
    {
        // Arrange
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };
        var fileUrls = new[]
        {
            "https://alfafile.net/file/online-code",
            "https://alfafile.net/file/offline-code",
        };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<string, bool>
                {
                    [fileUrls[0]] = true,
                    [fileUrls[1]] = false,
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[0]].ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[1]].ShouldBeFalse();
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InfoResponse { Status = (int)HttpStatusCode.OK });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_ApiReturnsUploadPipes_ReturnsPipeCount()
    {
        // Arrange
        var config = new AlfafileConfig { Username = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InfoResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new InfoResponse.ResponseObject
                    {
                        User = new LoginResponse.User
                        {
                            Upload = new LoginResponse.Upload { NbPipes = 2 },
                        },
                    },
                }
            );

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBe(2);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsAlfafileConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(
            new AlfafileConfig { Username = "user@example.test", Password = "password" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<AlfafileConfig>().Username.ShouldBe("user@example.test");
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
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;
    }
}
