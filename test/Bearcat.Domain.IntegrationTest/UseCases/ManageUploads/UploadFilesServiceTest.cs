using System.Reflection;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
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

        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            CreateTimeProvider()
        );

        service = new UploadFilesService(
            new UploadFilesRepository(dbContext, dbContext, NoOpSecretProtector.Instance),
            hosterFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            Mock.Of<ILogger<UploadFilesService>>(),
            notificationService,
            new HosterCaptchaVerificationService(notificationService)
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
    public async Task ProcessAsync_PendingUploadWithInactiveHosterRegistration_DoesNotProcessUpload()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        var upload = await AddUploadAsync(
            UploadState.Pending,
            [archiveFilePath],
            hosterIsActive: false
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.ShouldBeEmpty();
        hosterFactoryMock.Verify(f => f.GetHostersByName(), Times.Never);
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
        result.Notifications.Single().Message.ShouldBe(
            "The archive assigned upload has missing files, triggering re-packaging"
        );
        archive.ArchiveState.ShouldBe(ArchiveState.MissingFiles);
        hosterFactoryMock.Verify(f => f.GetHostersByName(), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_UnmanagedArchiveFileIsMissing_MarksArchiveMissingFilesAndUploadWaitingForArchive()
    {
        // Arrange
        var missingArchiveFilePath = Path.Combine(archiveFilesBasePath, "missing.part1.rar");
        var upload = await AddUploadAsync(
            UploadState.Pending,
            [missingArchiveFilePath],
            releaseType: ReleaseType.Unmanaged
        );

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
        result.ErrorMessages.ShouldBeEmpty();
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Warning);
        result.Notifications.Single().Message.ShouldBe(
            "The archive assigned upload has missing files. Refresh the unmanaged archive after providing the archive files."
        );
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

    [Test]
    public async Task ProcessAsync_PendingUploadAppearsWhileQueueRuns_AddsUploadToQueue()
    {
        // Arrange
        var firstArchiveFilePath = CreateArchiveFile("archive.part1.rar");
        var secondArchiveFilePath = CreateArchiveFile("archive.part2.rar");
        await AddUploadAsync(UploadState.Pending, [firstArchiveFilePath]);
        var secondUpload = await AddUploadAsync(
            UploadState.WaitingForArchive,
            [secondArchiveFilePath]
        );
        var secondUploadQueued = false;

        service.UploadQueuePollDelay = TimeSpan.FromMilliseconds(1);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.IsAny<FileDto>(),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                {
                    if (fileDto.FullFileName == firstArchiveFilePath && !secondUploadQueued)
                    {
                        await using var updateDbContext = CreateDbContext();
                        var upload = await updateDbContext.Uploads.SingleAsync(u =>
                            u.Id == secondUpload.Id
                        );
                        upload.UploadState = UploadState.Pending;
                        await updateDbContext.SaveChangesAsync();
                        secondUploadQueued = true;

                        await Task.Delay(50);
                    }

                    return new UploadFileResult(
                        true,
                        fileDto,
                        [],
                        $"https://hoster.test/{Path.GetFileName(fileDto.FullFileName)}"
                    );
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var uploads = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .OrderBy(u => u.Id)
            .ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.ShouldAllBe(u => u.UploadState == UploadState.Completed);
        uploads.ShouldAllBe(u => u.UploadedFiles.Count == 1);
    }

    [Test]
    public async Task ProcessAsync_PendingUploadAlreadyHasAllFilesUploaded_CompletesWithoutUploading()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        await AddUploadAsync(
            UploadState.Pending,
            [archiveFilePath],
            alreadyUploadedFileNames: [archiveFilePath]
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
                .ThenInclude(f => f.ArchiveFile)
            .SingleAsync();

        result.UploadState.ShouldBe(UploadState.Completed);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().ArchiveFile.FullFileName.ShouldBe(archiveFilePath);
        hosterMock.Verify(
            h =>
                h.UploadFileAsync(
                    It.IsAny<FileDto>(),
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_MorePendingUploadsThanGlobalLimit_SchedulesRemainingUploadsLater()
    {
        // Arrange
        var startedUploads = 0;
        var releaseUploads = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var archiveFilePaths = Enumerable
            .Range(1, 11)
            .Select(index => CreateArchiveFile($"archive.part{index}.rar"))
            .ToList();

        foreach (var archiveFilePath in archiveFilePaths)
        {
            await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        }

        service.UploadQueuePollDelay = TimeSpan.FromMilliseconds(1);
        hosterMock
            .Setup(h =>
                h.GetMaximumParallelUploadsAsync(hosterConfigMock.Object, CancellationToken.None)
            )
            .ReturnsAsync(20);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.IsAny<FileDto>(),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                {
                    if (Interlocked.Increment(ref startedUploads) == 10)
                    {
                        releaseUploads.SetResult();
                    }

                    await releaseUploads.Task;

                    return new UploadFileResult(
                        true,
                        fileDto,
                        [],
                        $"https://hoster.test/{Path.GetFileName(fileDto.FullFileName)}"
                    );
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var uploads = await dbContext.Uploads.Include(u => u.UploadedFiles).ToListAsync();

        startedUploads.ShouldBe(11);
        uploads.Count.ShouldBe(11);
        uploads.ShouldAllBe(u => u.UploadState == UploadState.Completed);
    }

    [Test]
    public async Task ProcessAsync_HosterThrowsException_StoresFailedUploadedFileAndFailsUpload()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("Hoster exploded"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.UploadState.ShouldBe(UploadState.Failed);
        result.UploadedFiles.Single().ErrorMessages.ShouldBe(["Hoster exploded"]);
    }

    [Test]
    public async Task ProcessAsync_FailedUploadResultAfterCancellationRequest_CancelsUpload()
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
                    await using var updateDbContext = CreateDbContext();
                    var uploadToCancel = await updateDbContext.Uploads.SingleAsync(u =>
                        u.Id == upload.Id
                    );
                    uploadToCancel.UploadState = UploadState.CancellationRequested;
                    await updateDbContext.SaveChangesAsync();

                    return new UploadFileResult(false, fileDto, ["Canceled"], null);
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.UploadState.ShouldBe(UploadState.Canceled);
        result.OnlineState.ShouldBe(OnlineState.Unknown);
        result.UploadedFiles.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_CancellationRequestedForUploadOutsideQueue_CancelsUpload()
    {
        // Arrange
        var runningArchiveFilePath = CreateArchiveFile("archive.part1.rar");
        var otherArchiveFilePath = CreateArchiveFile("archive.part2.rar");
        await AddUploadAsync(UploadState.Pending, [runningArchiveFilePath]);
        var otherUpload = await AddUploadAsync(
            UploadState.WaitingForArchive,
            [otherArchiveFilePath]
        );
        var otherCancellationRequested = false;

        service.UploadQueuePollDelay = TimeSpan.FromMilliseconds(1);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == runningArchiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (FileDto fileDto, IHosterConfig _, CancellationToken _) =>
                {
                    if (!otherCancellationRequested)
                    {
                        await using var updateDbContext = CreateDbContext();
                        var uploadToCancel = await updateDbContext.Uploads.SingleAsync(u =>
                            u.Id == otherUpload.Id
                        );
                        uploadToCancel.UploadState = UploadState.CancellationRequested;
                        await updateDbContext.SaveChangesAsync();
                        otherCancellationRequested = true;

                        await Task.Delay(50);
                    }

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
        var canceledUpload = await dbContext
            .Uploads.Include(u => u.Notifications)
            .SingleAsync(u => u.Id == otherUpload.Id);

        canceledUpload.UploadState.ShouldBe(UploadState.Canceled);
        canceledUpload.OnlineState.ShouldBe(OnlineState.Unknown);
        canceledUpload.Notifications.Single().Message.ShouldBe("Upload canceled");
    }

    [Test]
    public async Task ProcessAsync_ProcessCancellationDuringWorker_ThrowsOperationCanceledException()
    {
        // Arrange
        var archiveFilePath = CreateArchiveFile("archive.part1.rar");
        await AddUploadAsync(UploadState.Pending, [archiveFilePath]);
        using var cancellationTokenSource = new CancellationTokenSource();
        hosterMock
            .Setup(h =>
                h.GetMaximumParallelUploadsAsync(
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(1);
        hosterMock
            .Setup(h =>
                h.UploadFileAsync(
                    It.Is<FileDto>(f => f.FullFileName == archiveFilePath),
                    hosterConfigMock.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (FileDto _, IHosterConfig _, CancellationToken _) =>
                {
                    cancellationTokenSource.Cancel();
                    throw new OperationCanceledException(cancellationTokenSource.Token);
                }
            );

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.ProcessAsync(cancellationTokenSource.Token)
        );
    }

    [Test]
    public async Task HandleCompletedUploadTasksAsync_WorkerFails_LogsAndRemovesTask()
    {
        // Arrange
        var runningUploadTasks = new List<Task>
        {
            Task.FromException(new InvalidOperationException("Worker failed")),
        };

        // Act
        await InvokePrivateTaskAsync(
            service,
            "HandleCompletedUploadTasksAsync",
            runningUploadTasks,
            CancellationToken.None
        );

        // Assert
        runningUploadTasks.ShouldBeEmpty();
    }

    [Test]
    public async Task HandleCompletedUploadTasksAsync_ProcessCancellationWasRequested_RethrowsCancellation()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var runningUploadTasks = new List<Task>
        {
            Task.FromCanceled(cancellationTokenSource.Token),
        };

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await InvokePrivateTaskAsync(
                service,
                "HandleCompletedUploadTasksAsync",
                runningUploadTasks,
                cancellationTokenSource.Token
            )
        );
    }

    [Test]
    public async Task HandleCancellationRequestsAsync_ContextHasNoOpenWork_CancelsAndRemovesContext()
    {
        // Arrange
        var upload = await AddUploadAsync(UploadState.CancellationRequested, []);
        dbContext.ChangeTracker.Clear();
        var trackedUpload = await dbContext
            .Uploads.Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.HosterRegistration)
            .SingleAsync(u => u.Id == upload.Id);
        var context = new UploadExecutionContext(
            trackedUpload,
            totalFileCount: 1,
            successfulFileCount: 0,
            failedFileCount: 0,
            CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None)
        );
        var uploadContexts = new Dictionary<int, UploadExecutionContext>
        {
            [trackedUpload.Id] = context,
        };

        // Act
        await InvokePrivateTaskAsync(
            service,
            "HandleCancellationRequestsAsync",
            uploadContexts,
            CancellationToken.None
        );

        // Assert
        uploadContexts.ShouldBeEmpty();
        trackedUpload.UploadState.ShouldBe(UploadState.Canceled);
        trackedUpload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task HandleFileUploadResultAsync_ContextDoesNotExist_IgnoresResult()
    {
        // Arrange
        var uploadContexts = new Dictionary<int, UploadExecutionContext>();
        var result = new FileUploadCompleted(
            UploadId: 42,
            ArchiveFileId: 100,
            FullFileName: "/tmp/archive.part1.rar",
            FileUrl: null,
            IsSuccess: false,
            Errors: ["Ignored"]
        );

        // Act
        await InvokePrivateTaskAsync(
            service,
            "HandleFileUploadResultAsync",
            result,
            uploadContexts,
            CancellationToken.None
        );

        // Assert
        uploadContexts.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_CancellationRequestedUploadIsMissing_SkipsCancellation()
    {
        // Arrange
        var repository = new MissingCancellationUploadRepository();
        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            CreateTimeProvider()
        );

        var serviceWithMissingUpload = new UploadFilesService(
            repository,
            hosterFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            Mock.Of<ILogger<UploadFilesService>>(),
            notificationService,
            new HosterCaptchaVerificationService(notificationService)
        )
        {
            UploadQueuePollDelay = TimeSpan.Zero,
            NewPendingUploadsPollDelay = TimeSpan.Zero,
        };

        // Act
        await serviceWithMissingUpload.ProcessAsync(CancellationToken.None);

        // Assert
        repository.GetMissingUploadByIdWasCalled.ShouldBeTrue();
    }

    [Test]
    public async Task HandleNonExistingArchiveFilesAsync_UploadHasNoArchive_ReturnsFalse()
    {
        // Arrange
        var upload = new Upload
        {
            Id = 1,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Pending,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
        };

        // Act
        var result = await InvokePrivateTaskAsync<bool>(
            service,
            "HandleNonExistingArchiveFilesAsync",
            upload,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeFalse();
    }

    private async Task<Upload> AddUploadAsync(
        UploadState uploadState,
        IReadOnlyList<string> archiveFileNames,
        IReadOnlyList<string>? alreadyUploadedFileNames = null,
        ReleaseType releaseType = ReleaseType.Managed,
        bool hosterIsActive = true
    )
    {
        var uploadConfig = await AddUploadConfigAsync(releaseType, hosterIsActive);
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

    private async Task<UploadConfig> AddUploadConfigAsync(
        ReleaseType releaseType = ReleaseType.Managed,
        bool hosterIsActive = true
    )
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
            ReleaseType = releaseType,
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
            IsActive = hosterIsActive,
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

    private static async Task InvokePrivateTaskAsync(
        object target,
        string methodName,
        params object[] parameters
    )
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var task = (Task)method.Invoke(target, parameters)!;
        await task;
    }

    private static async Task<T> InvokePrivateTaskAsync<T>(
        object target,
        string methodName,
        params object[] parameters
    )
    {
        var method = target
            .GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        var task = (Task<T>)method.Invoke(target, parameters)!;
        return await task;
    }

    private sealed class MissingCancellationUploadRepository : IUploadFilesRepository
    {
        public bool GetMissingUploadByIdWasCalled { get; private set; }

        public Task<IReadOnlyList<Upload>> GetPendingUploadsAsync(
            IReadOnlySet<int> uploadIdsToExclude,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyList<Upload>>([]);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<IReadOnlyList<Upload>> GetOrphanedUploadsAsync(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyList<Upload>>([]);
        }

        public Task<IReadOnlyList<int>> GetCancellationRequestedUploadIdsAsync(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyList<int>>([42]);
        }

        public Task<bool> IsCancellationRequestedAsync(
            int uploadId,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(false);
        }

        public Task<Upload?> GetUploadByIdAsync(int uploadId, CancellationToken cancellationToken)
        {
            GetMissingUploadByIdWasCalled = true;
            return Task.FromResult<Upload?>(null);
        }

        public void ClearChangeTracker() { }

        public Task<IReadOnlyDictionary<int, string>> GetConfigByHosterRegistrationId(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        }

        public Task<IReadOnlyDictionary<string, string>> GetConfigByHosterClassName(
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>()
            );
        }
    }
}
