using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.UploadExecution.UploadPreparation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads.UploadExecution.UploadPreparation;

public class UploadHosterFolderServiceTest
{
    private const string ReleaseName = "Bearcat.Release.001";

    private const int ReleaseId = 7;

    private const int ArchiveId = 11;

    private const int UploadId = 13;

    private readonly IHosterConfig hosterConfig = Mock.Of<IHosterConfig>();

    private readonly UploadHosterFolderService service = new(
        NullLogger<UploadHosterFolderService>.Instance
    );

    [Test]
    public async Task CreateUploadFolderWhenHosterSupportsFoldersAsync_UploadOfArchive_UsesReleaseIdFolderName()
    {
        // Arrange
        var hosterMock = new Mock<IHosterWithFolders>();
        var upload = CreateUpload(ArchiveId);

        // Act
        var folderName = await CreateFolderAndGetFolderNameAsync(hosterMock, upload);

        // Assert
        folderName.ShouldBe($"{ReleaseName}_ReleaseId_{ReleaseId}");
    }

    private async Task<string?> CreateFolderAndGetFolderNameAsync(
        Mock<IHosterWithFolders> hosterMock,
        Upload upload
    )
    {
        string? createdFolderName = null;

        hosterMock
            .Setup(h =>
                h.CreateFolderAsync(It.IsAny<string>(), hosterConfig, It.IsAny<CancellationToken>())
            )
            .Callback<string, IHosterConfig, CancellationToken>(
                (folderName, _, _) => createdFolderName = folderName
            )
            .ReturnsAsync("folder-id");

        await service.CreateUploadFolderWhenHosterSupportsFoldersAsync(
            upload: upload,
            hoster: hosterMock.Object,
            hosterConfig: hosterConfig,
            hasFilesToUpload: true,
            cancellationToken: CancellationToken.None
        );

        return createdFolderName;
    }

    private static Upload CreateUpload(int? archiveId)
    {
        return new Upload
        {
            Id = UploadId,
            ArchiveId = archiveId,
            UploadConfig = new UploadConfig
            {
                ReleaseId = ReleaseId,
                Release = new Release { Name = ReleaseName },
            },
        };
    }
}
