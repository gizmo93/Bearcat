using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class UploadFilesServiceTest : BearcatIntegrationTest
{
    private const string HosterClassName = "TestHoster";
    private const string SerializedHosterConfig = "{\"apiKey\":\"test\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<IHoster> hosterMock = null!;
    private Mock<IHosterConfig> hosterConfigMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private string archiveFilesBasePath = null!;
    private string releaseFolderPath = null!;
    private string tempRootPath = null!;
    private UploadFilesService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "release"))
            .FullName;
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "archives"))
            .FullName;

        hosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        hosterMock = new Mock<IHoster>(MockBehavior.Strict);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedHosterConfig))
            .Returns(hosterConfigMock.Object);
        hosterMock
            .Setup(h =>
                h.GetMaximumParallelUploadsAsync(hosterConfigMock.Object, CancellationToken.None)
            )
            .ReturnsAsync(1);

        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(f => f.GetHostersByName())
            .Returns(new Dictionary<string, IHoster> { [HosterClassName] = hosterMock.Object });

        service = new UploadFilesService(
            new UploadFilesRepository(dbContext, dbContext),
            hosterFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            Mock.Of<ILogger<UploadFilesService>>(),
            new NotificationService(new NotificationRepository(dbContext), CreateTimeProvider())
        )
        {
            UploadQueuePollDelay = TimeSpan.Zero,
            NewPendingUploadsPollDelay = TimeSpan.Zero,
        };
    }

    [TearDown]
    public async Task DisposeResourcesAsync()
    {
        await dbContext.DisposeAsync();

        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_OrphanedUploadExists_ResetsUploadToPending()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.Uploading, []);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ProcessAsync_PendingUploadWithExistingArchiveFiles_UploadsFilesAndCompletesUpload()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var upload = await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                    new UploadFileResult(true, fileDto, [], "https://hoster.test/archive.part1.rar")
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Completed);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedAt.ShouldNotBeNull();
        result
            .UploadedFiles.Single()
            .HosterFileLink.ShouldBe("https://hoster.test/archive.part1.rar");
        result.UploadedFiles.Single().OnlineState.ShouldBe(OnlineState.Online);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Info);
        result.Notifications.Single().Message.ShouldBe("All files uploaded successfully");
        VerifyUploadPipelineCalled(archiveFilePath);
    }

    [Test]
    public async Task ProcessAsync_HosterUploadFails_MarksUploadFailedAndCreatesErrorNotification()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var upload = await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                    new UploadFileResult(false, fileDto, ["Upload failed"], null)
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Failed);
        result.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
        result.UploadedAt.ShouldBeNull();
        result.UploadedFiles.Single().HosterFileLink.ShouldBeEmpty();
        result.UploadedFiles.Single().ErrorMessages.ShouldBe(["Upload failed"]);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Error);
        result.Notifications.Single().Message.ShouldBe("Some files failed to upload");
        VerifyUploadPipelineCalled(archiveFilePath);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFileIsMissing_MarksArchiveMissingFilesAndUploadWaitingForArchive()
    {
        // Arrange
        var missingArchiveFilePath = Path.Combine(archiveFilesBasePath, "missing.part1.rar");
        var upload = await AddUploadAsync(UploadState.Pending, [missingArchiveFilePath]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.Archive)
            .Include(u => u.Notifications)
            .SingleAsync();
        var archive = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Warning);
        archive.ArchiveState.ShouldBe(ArchiveState.MissingFiles);
        hosterFactoryMock.Verify(f => f.GetHostersByName(), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_CancellationRequestedUploadExists_MarksUploadCanceled()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var upload = await AddUploadAsync(UploadState.CancellationRequested, [archiveFilePath]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Canceled);
        result.OnlineState.ShouldBe(OnlineState.Unknown);
        result.UploadedFiles.ShouldBeEmpty();
        result.Notifications.Single().Message.ShouldBe("Upload canceled");
        hosterFactoryMock.Verify(f => f.GetHostersByName(), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_CancellationRequestedDuringUpload_CancelsUpload()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var secondArchiveFilePath = CreateArchiveFile("archive.part2.rar");
        var upload = await AddUploadAsync(
            UploadState.Pending,
            [archiveFilePath, secondArchiveFilePath]
        );
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f =>
                        f.FullFileName == archiveFilePath || f.FullFileName == secondArchiveFilePath
                    ),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (FileDto fileDto, IHosterConfig _, CancellationToken cancellationToken) =>
                {
                    await using var cancellationDbContext = CreateDbContext();
                    var uploadToCancel = await cancellationDbContext.Uploads.SingleAsync(u =>
                        u.Id == upload.Id
                    );
                    uploadToCancel.UploadState = UploadState.CancellationRequested;
                    await cancellationDbContext.SaveChangesAsync();

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    return new UploadFileResult(
                        true,
                        fileDto,
                        [],
                        "https://hoster.test/archive.part1.rar"
                    );
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Canceled);
        result.OnlineState.ShouldBe(OnlineState.Unknown);
        result.UploadedFiles.ShouldBeEmpty();
        result.Notifications.Select(n => n.Message).ShouldContain("Upload canceled");
        hosterMock.Verify(
            h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f =>
                        f.FullFileName == archiveFilePath || f.FullFileName == secondArchiveFilePath
                    ),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_CancellationRequestedAfterSuccessfulFileUpload_CompletesUpload()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var upload = await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                {
                    await using var cancellationDbContext = CreateDbContext();
                    var uploadToCancel = await cancellationDbContext.Uploads.SingleAsync(u =>
                        u.Id == upload.Id
                    );
                    uploadToCancel.UploadState = UploadState.CancellationRequested;
                    await cancellationDbContext.SaveChangesAsync();

                    return new UploadFileResult(
                        true,
                        fileDto,
                        [],
                        "https://hoster.test/archive.part1.rar"
                    );
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Completed);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result
            .UploadedFiles.Single()
            .HosterFileLink.ShouldBe("https://hoster.test/archive.part1.rar");
        result
            .Notifications.Select(n => n.Message)
            .ShouldContain("All files uploaded successfully");
    }

    [Test]
    public async Task ProcessAsync_UploadAlreadyHasOneUploadedFile_UploadsOnlyMissingArchiveFiles()
    {
        // Arrange
        var alreadyUploadedFilePath = CreateArchiveFile("archive.part1.rar");
        var missingUploadedFilePath = CreateArchiveFile("archive.part2.rar");
        await AddUploadAsync(
            UploadState.Pending,
            [alreadyUploadedFilePath, missingUploadedFilePath],
            alreadyUploadedFileNames: [alreadyUploadedFilePath]
        );
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == missingUploadedFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                    new UploadFileResult(true, fileDto, [], "https://hoster.test/archive.part2.rar")
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
                .ThenInclude(f => f.ArchiveFile)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.UploadState.ShouldBe(UploadState.Completed);
        result.UploadedFiles.Count.ShouldBe(2);
        result
            .UploadedFiles.Select(f => f.ArchiveFile.FullFileName)
            .ShouldBe([alreadyUploadedFilePath, missingUploadedFilePath], ignoreOrder: true);
        hosterMock.Verify(
            h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == alreadyUploadedFilePath),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        VerifyUploadPipelineCalled(missingUploadedFilePath);
    }

    private async Task<Upload> AddUploadAsync(
        UploadState uploadState,
        IReadOnlyList<string> archiveFileNames,
        IReadOnlyList<string>? alreadyUploadedFileNames = null
    )
    {
        var uploadConfig = await AddUploadConfigAsync();
        var archive = new Archive
        {
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            ArchiveFolderPath = archiveFilesBasePath,
            ArchiveState = ArchiveState.Created,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = archiveFileNames
                .Select(fileName => new ArchiveFile { FullFileName = fileName })
                .ToList(),
            Uploads = [],
            ErrorMessages = [],
        };
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            Archive = archive,
            CreatedAt = DateTime.UtcNow,
            UploadState = uploadState,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        foreach (var fileName in alreadyUploadedFileNames ?? [])
        {
            var archiveFile = archive.ArchiveFiles.Single(f => f.FullFileName == fileName);
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    ArchiveFile = archiveFile,
                    HosterFileLink = $"https://hoster.test/{Path.GetFileName(fileName)}",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                    CheckedAt = DateTime.UtcNow,
                }
            );
        }

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

        return upload;
    }

    private void VerifyUploadPipelineCalled(string archiveFilePath)
    {
        hosterFactoryMock.Verify(f => f.GetHostersByName(), Times.Once);
        hosterMock.Verify(
            h => h.DeserializeHosterConfig(SerializedHosterConfig),
            Times.AtLeastOnce
        );
        hosterMock.Verify(
            h => h.GetMaximumParallelUploadsAsync(hosterConfigMock.Object, CancellationToken.None),
            Times.Once
        );
        hosterMock.Verify(
            h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private async Task<UploadConfig> AddUploadConfigAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = SerializedHosterConfig,
            HosterClassName = HosterClassName,
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
            LinksDistributedTo = [],
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private string CreateArchiveFile(string fileName)
    {
        var filePath = Path.Combine(archiveFilesBasePath, fileName);
        File.WriteAllText(filePath, "archive");

        return filePath;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
