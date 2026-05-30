using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Keep2Share;
using Bearcat.Hosters.Keep2Share.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Keep2Share;

public class Keep2ShareTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IKeep2ShareApiClient> apiClientMock = null!;
    private Hosters.Keep2Share.Keep2Share service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IKeep2ShareApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Keep2Share.Keep2Share>>();
        service = new Hosters.Keep2Share.Keep2Share(apiClientMock.Object, loggerMock.Object);
        service.UploadRetryDelay = TimeSpan.Zero;
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
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };
        var uploadFormData = new UploadFormDataResponse
        {
            Status = "success",
            Code = (int)HttpStatusCode.OK,
            FormAction = "https://upload.keep2share.test",
            FileField = "file",
            FormData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """{"ajax":true,"signature":"signature"}"""
            )!,
        };

        apiClientMock
            .Setup(x => x.RequestUploadAsync(config, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadFormData);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    uploadFormData,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = "success",
                    Success = true,
                    StatusCode = (int)HttpStatusCode.OK,
                    UserFileId = "file-id",
                    Link = "http://k2s.cc/file/file-id",
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("http://k2s.cc/file/file-id");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_FolderId_RequestsUploadFormForFolder()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(
            Id: 20,
            FullFileName: filePath,
            UploadId: 120,
            FolderId: "folder-id"
        );
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };
        var uploadFormData = new UploadFormDataResponse
        {
            Status = "success",
            Code = (int)HttpStatusCode.OK,
            FormAction = "https://upload.keep2share.test",
            FileField = "file",
            FormData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                """{"ajax":true,"signature":"signature"}"""
            )!,
        };

        apiClientMock
            .Setup(x => x.RequestUploadAsync(config, "folder-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadFormData);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    uploadFormData,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = "success",
                    Success = true,
                    StatusCode = (int)HttpStatusCode.OK,
                    UserFileId = "file-id",
                    Link = "http://k2s.cc/file/file-id",
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        apiClientMock.Verify(
            x => x.RequestUploadAsync(config, "folder-id", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task UploadFileAsync_RequestUploadFails_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 18, FullFileName: filePath, UploadId: 118);
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync(config, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("temporary upload error"));

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
            x => x.RequestUploadAsync(config, null, It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<UploadFormDataResponse>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task UploadFileAsync_CaptchaRequired_RethrowsWithoutRetrying()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 19, FullFileName: filePath, UploadId: 119);
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync(config, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));

        // Act + Assert
        await Should.ThrowAsync<CaptchaVerificationRequiredException>(
            () => service.UploadFileAsync(fileDto, config, CancellationToken.None)
        );
        apiClientMock.Verify(
            x => x.RequestUploadAsync(config, null, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task CreateFolderAsync_Config_CreatesFolderWithApiClient()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.CreateFolderAsync(config, "release-folder", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("folder-id");

        // Act
        var result = await service.CreateFolderAsync(
            "release-folder",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsStatuses_ReturnsStatuses()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };
        var fileUrls = new[]
        {
            "http://k2s.cc/file/online-code",
            "http://k2s.cc/file/offline-code",
        };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<string, bool> { [fileUrls[0]] = true, [fileUrls[1]] = false }
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
    public async Task CheckFilesExistAsync_ApiThrows_ReturnsFailure()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };
        var fileUrls = new[] { "http://k2s.cc/file/file-code" };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("keep2share unavailable"));

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["keep2share unavailable"]);
    }

    [Test]
    public async Task TryLoginAsync_LoginReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.LoginAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_LoginReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        apiClientMock
            .Setup(x => x.LoginAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "error",
                    Code = (int)HttpStatusCode.Forbidden,
                    ErrorCode = 77,
                    Message = "Network banned",
                }
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Network banned");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_Config_ReturnsStaticLimit()
    {
        // Arrange
        var config = new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" };

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBe(10);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsKeep2ShareConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(
            new Keep2ShareConfig { EmailAddress = "user@example.test", Password = "password" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<Keep2ShareConfig>().EmailAddress.ShouldBe("user@example.test");
        result.ShouldBeOfType<Keep2ShareConfig>().Password.ShouldBe("password");
    }

    [Test]
    public void UploadFormDataResponse_FormDataContainsBoolean_DeserializesFormData()
    {
        // Arrange
        const string rawJson =
            """
            {
              "status": "success",
              "code": 200,
              "form_action": "https://prx-22.filestore.app/upload",
              "file_field": "file",
              "form_data": {
                "ajax": true,
                "params": "params-value",
                "signature": "signature-value"
              }
            }
            """;

        // Act
        var result = JsonSerializer.Deserialize<UploadFormDataResponse>(
            rawJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        // Assert
        result.ShouldNotBeNull();
        result.FormData["ajax"].ValueKind.ShouldBe(JsonValueKind.True);
        result.FormData["params"].GetString().ShouldBe("params-value");
        result.FormData["signature"].GetString().ShouldBe("signature-value");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
