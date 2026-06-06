using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class UploadStateServiceTest : BearcatIntegrationTest
{
    private const string HosterClassName = "TestHoster";
    private const string SerializedHosterConfig = "{\"apiKey\":\"test\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<IHoster> hosterMock = null!;
    private Mock<IHosterConfig> hosterConfigMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private DateTime localNow;
    private UploadStateService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        localNow = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);

        hosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        hosterMock = new Mock<IHoster>(MockBehavior.Strict);
        hosterMock.SetupGet(h => h.Name).Returns(HosterClassName);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedHosterConfig))
            .Returns(hosterConfigMock.Object);

        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock.Setup(f => f.GetByName(HosterClassName)).Returns(hosterMock.Object);

        service = CreateService();
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterReportsAllFilesOnline_KeepsUploadOnline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        upload.UploadedFiles[0].ExternalId = "external-1";
        await dbContext.SaveChangesAsync();

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files =>
                        files.Count == 2
                        && files.Any(file =>
                            file.Url == "https://hoster.test/1" && file.ExternalId == "external-1"
                        )
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool>
                    {
                        ["https://hoster.test/1"] = true,
                        ["https://hoster.test/2"] = true,
                    }
                )
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.CheckedAt > localNow.AddMinutes(-1));
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_InactiveHosterRegistration_DoesNotCheckUploadState()
    {
        // Arrange
        var checkedAt = localNow.AddHours(-1);
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: checkedAt,
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterIsActive: false
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().CheckedAt.ShouldBe(checkedAt);
        hosterFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterReportsSomeFilesOffline_MarksUploadPartiallyOnlineAndCreatesWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool>
                    {
                        ["https://hoster.test/1"] = true,
                        ["https://hoster.test/2"] = false,
                    }
                )
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
        result.UploadedFiles.Count(f => f.OnlineState == OnlineState.Offline).ShouldBe(1);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Warning);
        result.Notifications.Single().Message.ShouldBe("Some files are offline on the hoster");
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterCheckFails_CreatesErrorNotificationAndKeepsState()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().OnlineState.ShouldBe(OnlineState.Online);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Error);
        result.Notifications.Single().Message.ShouldBe("Failed to check file existence on hoster.");
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_UploadConfigWithoutUploads_CreatesInitialWaitingForArchiveUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var result = await dbContext.Uploads.Include(u => u.Notifications).SingleAsync();

        result.ShouldNotBeNull();
        result.UploadConfigId.ShouldBe(uploadConfig.Id);
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.OnlineState.ShouldBe(OnlineState.Unknown);
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Info);
        result.Notifications.Single().Message.ShouldBe("Initial upload created for release");
    }

    [Test]
    public async Task CheckUploadStatesAsync_InactiveHosterRegistrationWithoutUploads_DoesNotCreateInitialUpload()
    {
        // Arrange
        await AddUploadConfigAsync(enableAutomaticReuploads: false, hosterIsActive: false);

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploadExists = await dbContext.Uploads.AnyAsync();

        uploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task CheckUploadStatesAsync_UploadConfigWithinInitialUploadCooldown_DoesNotCreateUpload()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            releaseCreatedAt: localNow.AddMinutes(-4)
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploadExists = await dbContext.Uploads.AnyAsync();

        uploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task CheckUploadStatesAsync_CustomInitialUploadCooldownIsMet_CreatesUpload()
    {
        // Arrange
        service = CreateService(initialUploadCooldownMinutes: 1);
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            releaseCreatedAt: localNow.AddMinutes(-2)
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var upload = await dbContext.Uploads.SingleAsync();

        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        upload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadIsDue_CreatesWaitingForArchiveUpload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.OrderBy(u => u.Id).ToListAsync();
        var reupload = result.Single(u => u.Id != upload.Id);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CheckUploadStatesAsync_InactiveHosterRegistrationAutomaticReuploadIsDue_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            hosterIsActive: false
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await dbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CheckUploadStatesAsync_CanceledUploadIsDueForAutomaticReupload_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );
        upload.UploadState = UploadState.Canceled;
        await dbContext.SaveChangesAsync();

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await dbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CheckUploadStatesAsync_OfflineUploadWithCanceledReupload_DoesNotCreateAnotherReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );
        var canceledReupload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            CreatedAt = localNow.AddHours(-24),
            UploadedAt = null,
            UploadState = UploadState.Canceled,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        dbContext.Uploads.Add(canceledReupload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await dbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Select(u => u.Id).ShouldBe([upload.Id, canceledReupload.Id]);
    }

    [Test]
    public async Task CreateManualReuploadAsync_OfflineUpload_CreatesWaitingForArchiveUpload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var reupload = await dbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateManualReuploadAsync_OnlineUpload_ThrowsInvalidOperationException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            "Manual reuploads can only be created for offline, partially online, canceled, or failed uploads."
        );
    }

    [Test]
    public async Task CreateManualReuploadAsync_CanceledUpload_CreatesWaitingForArchiveUpload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.Canceled;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var reupload = await dbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateManualReuploadAsync_FailedUpload_CreatesWaitingForArchiveUpload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.Failed;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var reupload = await dbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateManualReuploadAsync_BlockingUploadExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        dbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Pending,
                OnlineState = OnlineState.Unknown,
                ErrorMessages = [],
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            "A replacement upload already exists or is pending for this upload config."
        );
    }

    [Test]
    public async Task CreateManualReuploadAsync_OnlineBlockingUploadExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        dbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Completed,
                OnlineState = OnlineState.Online,
                ErrorMessages = [],
            }
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            "A replacement upload already exists or is pending for this upload config."
        );
    }

    [Test]
    public async Task CancelUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.CancelUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task CancelUploadAsync_UploadAlreadyHasCancellationRequested_ReturnsTrue()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.CancellationRequested;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task CancelUploadAsync_UploadCannotBeCanceled_ReturnsFalse()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    public async Task CancelUploadAsync_UploadCanBeCanceled_RequestsCancellation(
        UploadState uploadState
    )
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedUpload = await dbContext.Uploads.Include(u => u.Notifications).SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.UploadState.ShouldBe(UploadState.CancellationRequested);
        updatedUpload.Notifications.Single().Message.ShouldBe("Upload cancellation requested");
    }

    [Test]
    public async Task ResumeUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.ResumeUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task ResumeUploadAsync_CanceledUpload_SetsUploadPending()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = UploadState.Canceled;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedUpload = await dbContext.Uploads.SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.UploadState.ShouldBe(UploadState.Pending);
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.Completed)]
    [TestCase(UploadState.Failed)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task ResumeUploadAsync_NonCanceledUpload_ReturnsFalseAndKeepsState(
        UploadState uploadState
    )
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var updatedUpload = await dbContext.Uploads.SingleAsync();

        result.ShouldBeFalse();
        updatedUpload.UploadState.ShouldBe(uploadState);
    }

    [Test]
    public async Task DeleteUploadAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.DeleteUploadAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase(UploadState.Pending)]
    [TestCase(UploadState.Completed)]
    [TestCase(UploadState.Failed)]
    [TestCase(UploadState.Canceled)]
    public async Task DeleteUploadAsync_AllowedState_DeletesUpload(UploadState uploadState)
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        upload.UploadState = uploadState;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();

        result.ShouldBeTrue();
        (await dbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeFalse();
        (await dbContext.UploadedFiles.AnyAsync(f => f.UploadId == upload.Id)).ShouldBeFalse();
    }

    [TestCase(UploadState.WaitingForArchive)]
    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.CancellationRequested)]
    public async Task DeleteUploadAsync_DisallowedState_DoesNotDeleteUpload(UploadState uploadState)
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Unknown,
            checkedAt: null,
            uploadedFileLinks: []
        );
        upload.UploadState = uploadState;
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();

        result.ShouldBeFalse();
        (await dbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterReportsAllFilesOffline_MarksUploadOffline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool>
                    {
                        ["https://hoster.test/1"] = false,
                        ["https://hoster.test/2"] = false,
                    }
                )
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Offline);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
        result.Notifications.Single().Message.ShouldBe("Some files are offline on the hoster");
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadHasNoUploadedFiles_DoesNotCreateReupload()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: null,
            uploadedFileLinks: [],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await dbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadHasUncheckedFile_DoesNotCreateReupload()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: null,
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await dbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    private async Task<Upload> AddCompletedUploadAsync(
        OnlineState onlineState,
        DateTime? checkedAt,
        IReadOnlyList<string> uploadedFileLinks,
        bool enableAutomaticReuploads = false,
        bool hosterIsActive = true
    )
    {
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads, hosterIsActive);
        var archive = new Archive
        {
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            ArchiveFolderPath = "/tmp/archive",
            ArchiveState = ArchiveState.Created,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = uploadedFileLinks
                .Select(link => new ArchiveFile { FullFileName = $"{link}.rar" })
                .ToList(),
            Uploads = [],
            ErrorMessages = [],
        };
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            Archive = archive,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = localNow.AddHours(-2),
            UploadState = UploadState.Completed,
            OnlineState = onlineState,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        foreach (var fileLink in uploadedFileLinks)
        {
            upload.UploadedFiles.Add(
                new UploadedFile
                {
                    ArchiveFile = archive.ArchiveFiles[upload.UploadedFiles.Count],
                    HosterFileLink = fileLink,
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = localNow.AddHours(-2),
                    CheckedAt = checkedAt,
                }
            );
        }

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

        return upload;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        bool enableAutomaticReuploads,
        bool hosterIsActive = true,
        DateTime? releaseCreatedAt = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = enableAutomaticReuploads,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            CreatedAt = releaseCreatedAt ?? localNow.AddMinutes(-10),
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/tmp/archive",
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

    private UploadStateService CreateService(int initialUploadCooldownMinutes = 5)
    {
        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            CreateTimeProvider()
        );

        return new UploadStateService(
            new UploadStateRepository(dbContext),
            hosterFactoryMock.Object,
            CreateTimeProvider(),
            new TestApplicationConfigurationProvider(initialUploadCooldownMinutes),
            notificationService,
            new HosterCaptchaVerificationService(notificationService),
            Mock.Of<ILogger<UploadStateService>>(),
            NoOpSecretProtector.Instance
        );
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed class TestApplicationConfigurationProvider(int initialUploadCooldownMinutes)
        : IApplicationConfigurationProvider
    {
        public TConfiguration GetConfiguration<TConfiguration>()
            where TConfiguration : IApplicationConfiguration, new()
        {
            var configuration = new TConfiguration();

            if (configuration is InitialUploadConfiguration initialUploadConfiguration)
            {
                initialUploadConfiguration.CooldownMinutes = initialUploadCooldownMinutes;
            }

            return configuration;
        }

        public bool GetValue<TConfiguration>(
            Expression<Func<TConfiguration, bool>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new()
        {
            return GetValue<TConfiguration, bool>(propertySelector);
        }

        public int GetValue<TConfiguration>(Expression<Func<TConfiguration, int>> propertySelector)
            where TConfiguration : IApplicationConfiguration, new()
        {
            return GetValue<TConfiguration, int>(propertySelector);
        }

        public int? GetValue<TConfiguration>(
            Expression<Func<TConfiguration, int?>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new()
        {
            return GetValue<TConfiguration, int?>(propertySelector);
        }

        public string? GetValue<TConfiguration>(
            Expression<Func<TConfiguration, string?>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new()
        {
            return GetValue<TConfiguration, string?>(propertySelector);
        }

        public TValue GetValue<TConfiguration, TValue>(
            Expression<Func<TConfiguration, TValue>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new()
        {
            return propertySelector.Compile()(GetConfiguration<TConfiguration>());
        }
    }
}
