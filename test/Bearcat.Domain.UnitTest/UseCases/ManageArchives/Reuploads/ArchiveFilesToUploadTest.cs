using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Reuploads;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.Reuploads;

public class ArchiveFilesToUploadTest
{
    private UploadConfig firstUploadConfig = null!;
    private UploadConfig secondUploadConfig = null!;
    private List<ArchiveFile> archiveFiles = null!;

    [SetUp]
    public void SetUp()
    {
        firstUploadConfig = CreateUploadConfig(id: 21);
        secondUploadConfig = CreateUploadConfig(id: 22);
        archiveFiles =
        [
            new ArchiveFile { Id = 1, FullFileName = "/archives/archive.part1.rar" },
            new ArchiveFile { Id = 2, FullFileName = "/archives/archive.part2.rar" },
            new ArchiveFile { Id = 3, FullFileName = "/archives/archive.part3.rar" },
        ];
    }

    [Test]
    public void GetOnlineUploadedFilesToCarryOver_PreviousUploadHasOnlineAndOfflineFiles_ReturnsOnlyOnlineFilesWithLink()
    {
        // Arrange
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online),
            CreateUploadedFile(uploadId: 31, archiveFileId: 2, OnlineState.Offline),
            CreateUploadedFile(uploadId: 31, archiveFileId: 3, OnlineState.Online, link: " ")
        );

        // Act
        var result = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: waitingUpload,
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.Select(uploadedFile => uploadedFile.ArchiveFileId).ShouldBe([1]);
    }

    [Test]
    public void GetOnlineUploadedFilesToCarryOver_FileOfflineInNewestUploadButOnlineInOlderUpload_DoesNotReturnStaleOnlineFile()
    {
        // Arrange
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var olderUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online)
        );
        var newestUpload = CreateUpload(
            id: 32,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 32, archiveFileId: 1, OnlineState.Offline)
        );

        // Act
        var result = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: waitingUpload,
            uploadsOfArchive: [newestUpload, olderUpload]
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetOnlineUploadedFilesToCarryOver_PreviousUploadHasDifferentUploadConfig_ReturnsEmpty()
    {
        // Arrange
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: secondUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: waitingUpload,
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetOnlineUploadedFilesToCarryOver_UploadItselfIsPartOfArchiveUploads_IgnoresItsOwnFiles()
    {
        // Arrange
        var upload = CreateUpload(
            id: 40,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 40, archiveFileId: 1, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: upload,
            uploadsOfArchive: [upload]
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetOnlineUploadedFilesToCarryOver_HosterAlwaysReuploadsAllFiles_ReturnsEmpty()
    {
        // Arrange
        firstUploadConfig.HosterRegistration.AlwaysReuploadAllFiles = true;
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: waitingUpload,
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetArchiveFilesToUpload_SomeFilesAreCarriedOver_ReturnsOnlyTheOtherFiles()
    {
        // Arrange
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 2, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: archiveFiles,
            uploads: [waitingUpload],
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.Select(archiveFile => archiveFile.Id).ShouldBe([1, 3]);
    }

    [Test]
    public void GetArchiveFilesToUpload_UploadsCarryOverDifferentFiles_ReturnsUnionInArchiveFileOrder()
    {
        // Arrange
        var firstWaitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var secondWaitingUpload = CreateUpload(id: 41, uploadConfig: secondUploadConfig);
        var firstPreviousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online),
            CreateUploadedFile(uploadId: 31, archiveFileId: 2, OnlineState.Online)
        );
        var secondPreviousUpload = CreateUpload(
            id: 32,
            uploadConfig: secondUploadConfig,
            CreateUploadedFile(uploadId: 32, archiveFileId: 2, OnlineState.Online),
            CreateUploadedFile(uploadId: 32, archiveFileId: 3, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: archiveFiles,
            uploads: [secondWaitingUpload, firstWaitingUpload],
            uploadsOfArchive: [firstPreviousUpload, secondPreviousUpload]
        );

        // Assert
        result.Select(archiveFile => archiveFile.Id).ShouldBe([1, 3]);
    }

    [Test]
    public void GetArchiveFilesToUpload_AllFilesAreCarriedOver_ReturnsEmpty()
    {
        // Arrange
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online),
            CreateUploadedFile(uploadId: 31, archiveFileId: 2, OnlineState.Online),
            CreateUploadedFile(uploadId: 31, archiveFileId: 3, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: archiveFiles,
            uploads: [waitingUpload],
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetArchiveFilesToUpload_HosterAlwaysReuploadsAllFiles_ReturnsEveryArchiveFile()
    {
        // Arrange
        firstUploadConfig.HosterRegistration.AlwaysReuploadAllFiles = true;
        var waitingUpload = CreateUpload(id: 40, uploadConfig: firstUploadConfig);
        var previousUpload = CreateUpload(
            id: 31,
            uploadConfig: firstUploadConfig,
            CreateUploadedFile(uploadId: 31, archiveFileId: 1, OnlineState.Online),
            CreateUploadedFile(uploadId: 31, archiveFileId: 2, OnlineState.Online)
        );

        // Act
        var result = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: archiveFiles,
            uploads: [waitingUpload],
            uploadsOfArchive: [previousUpload]
        );

        // Assert
        result.ShouldBe(archiveFiles);
    }

    private static UploadConfig CreateUploadConfig(int id)
    {
        return new UploadConfig
        {
            Id = id,
            HosterRegistration = new HosterRegistration
            {
                Id = id + 100,
                Name = $"Hoster {id}",
                SerializedConfig = "{}",
                HosterClassName = $"Hoster{id}",
                IsActive = true,
            },
        };
    }

    private static Upload CreateUpload(
        int id,
        UploadConfig uploadConfig,
        params UploadedFile[] uploadedFiles
    )
    {
        return new Upload
        {
            Id = id,
            UploadConfigId = uploadConfig.Id,
            UploadConfig = uploadConfig,
            UploadedFiles = uploadedFiles.ToList(),
        };
    }

    private static UploadedFile CreateUploadedFile(
        int uploadId,
        int archiveFileId,
        OnlineState onlineState,
        string? link = null
    )
    {
        return new UploadedFile
        {
            Id = uploadId * 100 + archiveFileId,
            UploadId = uploadId,
            ArchiveFileId = archiveFileId,
            HosterFileLink = link ?? $"https://hoster.test/{uploadId}/{archiveFileId}",
            OnlineState = onlineState,
        };
    }
}
