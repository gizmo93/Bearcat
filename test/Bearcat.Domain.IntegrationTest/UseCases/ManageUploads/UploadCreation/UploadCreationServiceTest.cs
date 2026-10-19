using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
using Bearcat.Domain.UseCases.ManageUploads.UploadCreation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads.UploadCreation;

public class UploadCreationServiceTest(DatabaseProvider databaseProvider)
    : UploadIntegrationTest(databaseProvider)
{
    private UploadCreationService service = null!;

    [SetUp]
    public void SetUpService()
    {
        service = CreateService();
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_UploadConfigWithoutUploads_CreatesInitialWaitingForArchiveUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var result = await DbContext.Uploads.Include(u => u.Notifications).SingleAsync();

        result.ShouldNotBeNull();
        result.UploadConfigId.ShouldBe(uploadConfig.Id);
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.OnlineState.ShouldBe(OnlineState.Unknown);
        result.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Info);
        result.Notifications.Single().Message.ShouldBe("Initial upload created for release");
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_VideoUploadConfigWithoutUploads_CreatesPendingUploadWithoutArchive()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            uploadSource: UploadSource.VideoFiles
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var result = await DbContext.Uploads.SingleAsync();

        result.UploadConfigId.ShouldBe(uploadConfig.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.ArchiveId.ShouldBeNull();
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_InactiveHosterRegistrationWithoutUploads_DoesNotCreateInitialUpload()
    {
        // Arrange
        await AddUploadConfigAsync(enableAutomaticReuploads: false, hosterIsActive: false);

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var uploadExists = await DbContext.Uploads.AnyAsync();

        uploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_HosterAccountPoolUploadConfigWithActiveMember_CreatesInitialUploadWithoutHosterRegistration()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);
        await MoveUploadConfigToHosterAccountPoolAsync(uploadConfig, memberIsActive: true);

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync();

        result.UploadConfigId.ShouldBe(uploadConfig.Id);
        result.HosterRegistrationId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_HosterAccountPoolUploadConfigWithoutActiveMember_DoesNotCreateInitialUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);
        await MoveUploadConfigToHosterAccountPoolAsync(uploadConfig, memberIsActive: false);

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var uploadExists = await DbContext.Uploads.AnyAsync();

        uploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_UploadConfigWithinInitialUploadCooldown_DoesNotCreateUpload()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            releaseCreatedAt: LocalNow.AddMinutes(-4)
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var uploadExists = await DbContext.Uploads.AnyAsync();

        uploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_CustomInitialUploadCooldownIsMet_CreatesUpload()
    {
        // Arrange
        service = CreateService(initialUploadCooldownMinutes: 1);
        var uploadConfig = await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            releaseCreatedAt: LocalNow.AddMinutes(-2)
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        var upload = await DbContext.Uploads.SingleAsync();

        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        upload.OnlineState.ShouldBe(OnlineState.Unknown);
        upload.HosterRegistrationId.ShouldBe(uploadConfig.HosterRegistrationId);
    }

    [Test]
    public async Task CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync_ReleaseWithinInitialUploadCooldown_CreatesInitialUploadOnlyForThatRelease()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            releaseCreatedAt: CreateTimeProvider().GetLocalNow()
        );
        var otherUploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        var result = await service.CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync(
            uploadConfig.ReleaseId
        );

        // Assert
        result.ShouldBe(
            new InitialUploadCreationForReleaseResult(
                CreatedUploadCount: 1,
                IsBlockedByQualityGate: false
            )
        );
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.Include(u => u.Notifications).SingleAsync();
        upload.UploadConfigId.ShouldBe(uploadConfig.Id);
        upload.UploadConfigId.ShouldNotBe(otherUploadConfig.Id);
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        upload.Notifications.Single().Message.ShouldBe("Initial upload created for release");
    }

    [Test]
    public async Task CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync_QualityGateFails_DoesNotCreateUploadAndReportsQualityGate()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile()
        );

        // Act
        var result = await service.CreateMissingInitialUploadsForReleaseIgnoringCooldownAsync(
            uploadConfig.ReleaseId
        );

        // Assert
        result.ShouldBe(
            new InitialUploadCreationForReleaseResult(
                CreatedUploadCount: 0,
                IsBlockedByQualityGate: true
            )
        );
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeFalse();
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_MoreDueAutomaticReuploadsThanOneRunCreates_CreatesOneHundredReuploads()
    {
        // Arrange
        foreach (var uploadNumber in Enumerable.Range(1, 101))
        {
            await AddCompletedUploadAsync(
                OnlineState.Offline,
                checkedAt: LocalNow.AddHours(-25),
                uploadedFileLinks: [$"https://hoster.test/{uploadNumber}"],
                enableAutomaticReuploads: true
            );
        }

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var reuploadCount = await DbContext.Uploads.CountAsync(u =>
            u.UploadState == UploadState.WaitingForArchive
        );
        reuploadCount.ShouldBe(100);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadIsDue_CreatesWaitingForArchiveUpload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();
        var reupload = result.Single(u => u.Id != upload.Id);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_HosterHoursOverrideShorterThanReleaseGroup_CreatesReuploadEarlier()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-2),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            numberOfHoursUntilReuploadOverride: 1
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_OnlyWhenFullyOfflineTriggerAndPartiallyOnline_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            enableAutomaticReuploads: true,
            reuploadTriggerOverride: ReuploadTrigger.OnlyWhenFullyOffline
        );
        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
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
        var uploadOnlineCheckService = CreateUploadOnlineCheckService();

        // Act
        await uploadOnlineCheckService.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_OnlyWhenFullyOfflineTriggerAndFullyOffline_CreatesReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            reuploadTriggerOverride: ReuploadTrigger.OnlyWhenFullyOffline
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_InactiveHosterRegistrationAutomaticReuploadIsDue_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            hosterIsActive: false
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_CanceledUploadIsDueForAutomaticReupload_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );
        upload.UploadState = UploadState.Canceled;
        await DbContext.SaveChangesAsync();

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_OfflineUploadWithCanceledReupload_DoesNotCreateAnotherReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );
        var canceledReupload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            HosterRegistrationId = upload.HosterRegistrationId,
            CreatedAt = LocalNow.AddHours(-24),
            UploadedAt = null,
            UploadState = UploadState.Canceled,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(canceledReupload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Select(u => u.Id).ShouldBe([upload.Id, canceledReupload.Id]);
    }

    [Test]
    public async Task CreateManualReuploadAsync_OfflineUpload_CreatesWaitingForArchiveUploadWithoutNotification()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task CreateManualReuploadAsync_UploadOnOtherHosterRegistrationThanItsUploadConfig_CreatesReuploadOnHosterRegistrationOfUploadConfig()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var hosterRegistrationOfUploadConfig = upload.HosterRegistrationId;
        var previousHosterRegistration = CreateHosterRegistration();
        DbContext.HosterRegistrations.Add(previousHosterRegistration);
        await DbContext.SaveChangesAsync();
        upload.HosterRegistrationId = previousHosterRegistration.Id;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.HosterRegistrationId.ShouldBe(hosterRegistrationOfUploadConfig);
    }

    [Test]
    public async Task CreateManualReuploadAsync_UploadOfHosterAccountPoolUploadConfigOnActiveMember_CreatesReuploadOnSameAccount()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync(u =>
            u.Id == upload.UploadConfigId
        );
        await MoveUploadConfigToHosterAccountPoolAsync(uploadConfig, memberIsActive: true);
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.HosterRegistrationId.ShouldBe(upload.HosterRegistrationId);
    }

    [Test]
    public async Task CreateManualReuploadAsync_UploadOfHosterAccountPoolUploadConfigOnMemberRequiringCaptchaVerification_CreatesReuploadWithoutHosterRegistration()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync(u =>
            u.Id == upload.UploadConfigId
        );
        await MoveUploadConfigToHosterAccountPoolAsync(uploadConfig, memberIsActive: true);
        var memberRegistration = await DbContext.HosterRegistrations.SingleAsync(registration =>
            registration.Id == upload.HosterRegistrationId
        );
        memberRegistration.RequiresCaptchaVerification = true;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.HosterRegistrationId.ShouldBeNull();
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadOfHosterAccountPoolUploadConfigOnInactiveMember_CreatesReuploadWithoutHosterRegistration()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );
        var uploadConfig = await DbContext.UploadConfigs.SingleAsync(u =>
            u.Id == upload.UploadConfigId
        );
        await MoveUploadConfigToHosterAccountPoolAsync(uploadConfig, memberIsActive: false);
        uploadConfig.HosterAccountPool!.Members.Add(
            new HosterAccountPoolMember { HosterRegistration = CreateHosterRegistration() }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id != upload.Id);

        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.HosterRegistrationId.ShouldBeNull();
    }

    [Test]
    public async Task CreateManualReuploadAsync_OnlineUpload_ThrowsInvalidUploadStateException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await Should.ThrowAsync<InvalidUploadStateException>(async () =>
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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateManualReuploadAsync_BlockingUploadExists_ThrowsInvalidUploadStateException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
                HosterRegistrationId = upload.HosterRegistrationId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Pending,
                OnlineState = OnlineState.Unknown,
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidUploadStateException>(async () =>
            await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            "A replacement upload already exists or is pending for this upload config."
        );
    }

    [Test]
    public async Task CreateManualReuploadAsync_OnlineBlockingUploadExists_ThrowsInvalidUploadStateException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
                HosterRegistrationId = upload.HosterRegistrationId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Completed,
                OnlineState = OnlineState.Online,
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Should.ThrowAsync<InvalidUploadStateException>(async () =>
            await service.CreateManualReuploadAsync(upload.Id, CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            "A replacement upload already exists or is pending for this upload config."
        );
    }

    [Test]
    public async Task CreateManualReuploadsForReleasesAsync_UploadsWithDifferentOnlineStates_CreatesReuploadsOnlyForNotFullyOnlineUploadsOfGivenReleases()
    {
        // Arrange
        var partiallyOnlineUpload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var onlineUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/2"]
        );
        var offlineUploadOfOtherRelease = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/3"]
        );
        var releaseIds = await DbContext
            .UploadConfigs.Where(uc =>
                uc.Id == partiallyOnlineUpload.UploadConfigId
                || uc.Id == onlineUpload.UploadConfigId
            )
            .Select(uc => uc.ReleaseId)
            .ToListAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadsForReleasesAsync(
            releaseIds,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u =>
            u.Id != partiallyOnlineUpload.Id
            && u.Id != onlineUpload.Id
            && u.Id != offlineUploadOfOtherRelease.Id
        );

        result.ShouldBe(1);
        reupload.UploadConfigId.ShouldBe(partiallyOnlineUpload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CreateManualReuploadsForReleasesAsync_UploadConfigWithSeveralOfflineUploads_CreatesSingleReupload()
    {
        // Arrange
        var olderUpload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = olderUpload.UploadConfigId,
                HosterRegistrationId = olderUpload.HosterRegistrationId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.Completed,
                OnlineState = OnlineState.Offline,
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();
        var releaseId = await DbContext
            .UploadConfigs.Where(uc => uc.Id == olderUpload.UploadConfigId)
            .Select(uc => uc.ReleaseId)
            .SingleAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadsForReleasesAsync(
            [releaseId],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(1);
        (await DbContext.Uploads.CountAsync()).ShouldBe(3);
    }

    [Test]
    public async Task CreateManualReuploadsForReleasesAsync_ReplacementUploadIsPending_CreatesNoReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
                HosterRegistrationId = upload.HosterRegistrationId,
                CreatedAt = DateTime.UtcNow,
                UploadState = UploadState.WaitingForArchive,
                OnlineState = OnlineState.Unknown,
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();
        var releaseId = await DbContext
            .UploadConfigs.Where(uc => uc.Id == upload.UploadConfigId)
            .Select(uc => uc.ReleaseId)
            .SingleAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await service.CreateManualReuploadsForReleasesAsync(
            [releaseId],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
        (await DbContext.Uploads.CountAsync()).ShouldBe(2);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadHasNoUploadedFiles_DoesNotCreateReupload()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: null,
            uploadedFileLinks: [],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadNotFullyOnlineSinceWithinThreshold_DoesNotCreateReupload()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_PartiallyOnlineUploadStillBeingRechecked_CreatesReuploadBasedOnNotFullyOnlineSince()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow.AddMinutes(-31),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            enableAutomaticReuploads: true
        );
        upload.NotFullyOnlineSince = LocalNow.AddHours(-25);
        upload.UploadedFiles[1].OnlineState = OnlineState.Offline;
        await DbContext.SaveChangesAsync();

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
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
        var uploadOnlineCheckService = CreateUploadOnlineCheckService();

        // Act
        await uploadOnlineCheckService.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .OrderBy(u => u.Id)
            .ToListAsync();
        var recheckedUpload = result.Single(u => u.Id == upload.Id);
        var reupload = result.Single(u => u.Id != upload.Id);

        result.Count.ShouldBe(2);
        recheckedUpload.NotFullyOnlineSince.ShouldBe(LocalNow.AddHours(-25));
        recheckedUpload.UploadedFiles.ShouldContain(f => f.CheckedAt == LocalNow);
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_QualityGateFails_DoesNotCreateInitialUploadAndMarksFailed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile()
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeFalse();
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityGateEvaluatedAt.ShouldBe(LocalNow);
        release.QualityIssues.Single().Description.ShouldBe("NFO is missing");
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_QualityGatePasses_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile(),
            releaseInfo: CreateReleaseInfo(),
            hasNfo: true
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync();
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_ManuallyApprovedFailingRelease_CreatesUploadAndKeepsApproval()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile()
        );
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState = QualityGateState.ManuallyApproved;
        await DbContext.SaveChangesAsync();

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeTrue();
        var updated = await DbContext.Releases.SingleAsync();
        updated.QualityGateState.ShouldBe(QualityGateState.ManuallyApproved);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadDueButQualityGateFailed_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile()
        );
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState = QualityGateState.Failed;
        await DbContext.SaveChangesAsync();

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();
        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadDueAndQualityGatePasses_CreatesReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile(),
            releaseInfo: CreateReleaseInfo(),
            hasNfo: true
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();
        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadConfigId.ShouldBe(upload.UploadConfigId);
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
    }

    [Test]
    public async Task CreateAutomaticReuploadsForOfflineUploadsAsync_AutomaticReuploadDueButGateNotYetEvaluatedAndFails_SkipsAndStoresFailure()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: LocalNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile()
        );

        // Act
        await service.CreateAutomaticReuploadsForOfflineUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.ToListAsync();
        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityIssues.Single().Description.ShouldBe("NFO is missing");
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_ReleaseGroupWithoutProfile_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeTrue();
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
        release.QualityGateEvaluatedAt.ShouldBe(LocalNow);
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_UnmanagedReleaseWithoutNfo_DoesNotCreateInitialUploadAndMarksFailed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile(),
            releaseType: ReleaseType.Unmanaged
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeFalse();
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityIssues.Single().Description.ShouldBe("NFO is missing");
    }

    [Test]
    public async Task CreateMissingInitialUploadsAsync_UnmanagedReleaseWithOnlyFolderBasedRules_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateFolderBasedProfile(),
            releaseType: ReleaseType.Unmanaged
        );

        // Act
        await service.CreateMissingInitialUploadsAsync(LocalNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync();
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
        release.QualityIssues.ShouldBeEmpty();
    }

    private static QualityProfile CreateFolderBasedProfile() =>
        new()
        {
            Name = "Folder based",
            Rules =
            [
                new QualityCheckRule
                {
                    RuleType = QualityCheckRuleType.FilePatternPresent,
                    ParametersJson = QualityCheckParameterValues.Serialize(
                        new Dictionary<string, object?> { ["pattern"] = "*.nfo" }
                    ),
                },
                new QualityCheckRule
                {
                    RuleType = QualityCheckRuleType.MinimumFolderSize,
                    ParametersJson = QualityCheckParameterValues.Serialize(
                        new Dictionary<string, object?> { ["minimumMegabytes"] = 100 }
                    ),
                },
                new QualityCheckRule
                {
                    RuleType = QualityCheckRuleType.MediaInfoPresent,
                    ParametersJson = "{}",
                },
            ],
        };

    private static QualityProfile CreateRequireNfoProfile() =>
        new()
        {
            Name = "Require NFO",
            Rules =
            [
                new QualityCheckRule
                {
                    RuleType = QualityCheckRuleType.RequiredReleaseInfo,
                    ParametersJson = QualityCheckParameterValues.Serialize(
                        new Dictionary<string, object?>
                        {
                            ["requireCover"] = false,
                            ["requireDescription"] = false,
                            ["requireNfo"] = true,
                        }
                    ),
                },
            ],
        };

    private static ReleaseInfo CreateReleaseInfo() =>
        new()
        {
            NfoDatabaseClassName = ReleaseInfo.ManualSource,
            ReleaseName = "Bearcat.Release.001",
        };

    private async Task MoveUploadConfigToHosterAccountPoolAsync(
        UploadConfig uploadConfig,
        bool memberIsActive
    )
    {
        var memberRegistration = await DbContext.HosterRegistrations.SingleAsync(registration =>
            registration.Id == uploadConfig.HosterRegistrationId
        );
        memberRegistration.IsActive = memberIsActive;
        uploadConfig.HosterRegistrationId = null;
        uploadConfig.HosterRegistration = null;
        uploadConfig.HosterAccountPool = new HosterAccountPool
        {
            Name = "Pool",
            HosterClassName = HosterClassName,
            Members = [new HosterAccountPoolMember { HosterRegistration = memberRegistration }],
        };
        await DbContext.SaveChangesAsync();
    }

    private UploadCreationService CreateService(int initialUploadCooldownMinutes = 5)
    {
        return new UploadCreationService(
            new UploadStateRepository(DbContext),
            CreateTimeProvider(),
            new TestApplicationConfigurationProvider(initialUploadCooldownMinutes),
            CreateNotificationService(),
            Mock.Of<ILogger<UploadCreationService>>(),
            new QualityGateEvaluator(
                [
                    new FilePatternQualityCheck(),
                    new MinimumFolderSizeQualityCheck(),
                    new RequiredReleaseInfoQualityCheck(),
                    new MediaInfoQualityCheck(),
                ],
                new FileSystemService()
            )
        );
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

        public decimal? GetValue<TConfiguration>(
            Expression<Func<TConfiguration, decimal?>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new()
        {
            return GetValue<TConfiguration, decimal?>(propertySelector);
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
