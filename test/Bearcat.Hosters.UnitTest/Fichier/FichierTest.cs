using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Fichier;
using Bearcat.Hosters.Fichier.Api;
using Bearcat.Hosters.Fichier.Api.Upload;
using Bearcat.Hosters.Fichier.Api.User;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fichier;

public class FichierTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IFichierApiClient> apiClientMock = null!;
    private Hosters.Fichier.Fichier service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IFichierApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Fichier.Fichier>>();
        service = new Hosters.Fichier.Fichier(apiClientMock.Object, loggerMock.Object)
        {
            UploadRetryDelay = TimeSpan.Zero,
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
        var fileDto = new FileDto(Id: 31, FullFileName: filePath, UploadId: 131);
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EndUploadResponse
                {
                    Links =
                    [
                        new EndUploadResponse.UploadedLink
                        {
                            Download = "https://1fichier.com/?download",
                            Filename = Path.GetFileName(filePath),
                            Size = "14",
                        },
                    ],
                }
            );

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
        result.FileUrl.ShouldBe("https://1fichier.com/?download");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_ApiUploadThrows_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 32, FullFileName: filePath, UploadId: 132);
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("upload failed"));

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
        result.ErrorMessages.ShouldBe(["upload failed", "upload failed", "upload failed"]);
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }

    [Test]
    public async Task CheckFilesExistAsync_CredentialsRejected_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var fileUrls = new[] { "https://1fichier.com/?file" };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HosterCredentialsRejectedException("No such user #133"));

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            service.CheckFilesExistAsync(
                config,
                fileUrls.Select(url => new FileUrlToCheckDto(url, null)).ToList(),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("No such user #133");
    }

    [Test]
    public async Task TryLoginAsync_UserInfoReturnsKo_ReturnsFailure()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfoResponse { Status = "KO", Message = "invalid key" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
