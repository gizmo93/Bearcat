using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.IntegrationTest.Database.MaterializedValues;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class UploadStateRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime LocalNow = new(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);

    private UploadStateRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new UploadStateRepository(DbContext);
    }

    [Test]
    public async Task GetActiveHosterRegistrationsAsync_ActiveAndInactiveRegistrations_ReturnsActiveRegistrationsOrderedById()
    {
        // Arrange
        var firstActiveRegistration = await AddHosterRegistrationAsync("First", isActive: true);
        await AddHosterRegistrationAsync("Inactive", isActive: false);
        var secondActiveRegistration = await AddHosterRegistrationAsync("Second", isActive: true);

        // Act
        var result = await repository.GetActiveHosterRegistrationsAsync(CancellationToken.None);

        // Assert
        result
            .Select(registration => registration.Id)
            .ShouldBe([firstActiveRegistration.Id, secondActiveRegistration.Id]);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_UploadsOfSeveralHosterRegistrations_ReturnsOnlyUploadsOfGivenRegistration()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Checked", isActive: true);
        var otherRegistration = await AddHosterRegistrationAsync("Other", isActive: true);
        var upload = await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [null]
        );
        await AddUploadAsync(await AddUploadConfigAsync(otherRegistration), fileCheckedAts: [null]);

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_UploadOnOtherHosterRegistrationThanItsUploadConfig_ReturnsUploadOnlyForHosterRegistrationOfUpload()
    {
        // Arrange
        var registrationOfUploadConfig = await AddHosterRegistrationAsync(
            "Configured",
            isActive: true
        );
        var registrationOfUpload = await AddHosterRegistrationAsync("Holding", isActive: true);
        var upload = await AddUploadAsync(
            await AddUploadConfigAsync(registrationOfUploadConfig),
            fileCheckedAts: [null],
            hosterRegistrationOfUpload: registrationOfUpload
        );

        // Act
        var resultOfRegistrationOfUpload = await GetLeastRecentlyCheckedUploadsToCheckAsync(
            registrationOfUpload.Id
        );
        var resultOfRegistrationOfUploadConfig = await GetLeastRecentlyCheckedUploadsToCheckAsync(
            registrationOfUploadConfig.Id
        );

        // Assert
        resultOfRegistrationOfUpload.ShouldHaveSingleItem().Id.ShouldBe(upload.Id);
        resultOfRegistrationOfUploadConfig.ShouldBeEmpty();
    }

    [TestCase(null, true)]
    [TestCase(-31, true)]
    [TestCase(-29, false)]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_FileCheckedRelativeToNow_ReturnsUploadOnlyWhenFileWasNotCheckedWithinThirtyMinutes(
        int? checkedMinutesBeforeNow,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts:
            [
                checkedMinutesBeforeNow is null
                    ? null
                    : LocalNow.AddMinutes(checkedMinutesBeforeNow.Value),
            ]
        );

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(UploadState.Completed, OnlineState.Online, true)]
    [TestCase(UploadState.Completed, OnlineState.PartiallyOnline, true)]
    [TestCase(UploadState.Completed, OnlineState.Offline, false)]
    [TestCase(UploadState.Completed, OnlineState.Unknown, false)]
    [TestCase(UploadState.Failed, OnlineState.Online, false)]
    [TestCase(UploadState.Canceled, OnlineState.Online, false)]
    [TestCase(UploadState.CancellationRequested, OnlineState.Online, false)]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_UploadInState_ReturnsOnlyOnlineOrPartiallyOnlineUploadsThatAreNotFailedOrCanceled(
        UploadState uploadState,
        OnlineState onlineState,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [null],
            uploadState: uploadState,
            onlineState: onlineState
        );

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_OnlineUploadWithoutUploadedFiles_ReturnsNoUpload()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(await AddUploadConfigAsync(registration), fileCheckedAts: []);

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_SeveralUploadsWithNeverCheckedFiles_ReturnsTheOnesWithLowestIdsBeforeCheckedUploads()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-5)]
        );
        var firstNeverCheckedUpload = await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-1), null]
        );
        var secondNeverCheckedUpload = await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [null]
        );
        await AddUploadAsync(await AddUploadConfigAsync(registration), fileCheckedAts: [null]);

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(
            registration.Id,
            maximumUploadCount: 2
        );

        // Assert
        result
            .Select(upload => upload.Id)
            .ShouldBe([firstNeverCheckedUpload.Id, secondNeverCheckedUpload.Id]);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_OlderUploadSupersededByNewerCompletedUpload_ReturnsOnlyNewerUpload()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        var uploadConfig = await AddUploadConfigAsync(registration);
        await AddUploadAsync(uploadConfig, fileCheckedAts: [null]);
        var newerUpload = await AddUploadAsync(uploadConfig, fileCheckedAts: [null]);

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(newerUpload.Id);
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_MoreDueUploadsThanMaximumCount_ReturnsUploadsWithNeverCheckedFilesFirstAndThenLeastRecentlyCheckedUploads()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-2)]
        );
        var leastRecentlyCheckedUpload = await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-5)]
        );
        var uploadWithNeverCheckedFile = await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddMinutes(-40), null]
        );
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-1)]
        );

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(
            registration.Id,
            maximumUploadCount: 2
        );

        // Assert
        result
            .Select(upload => upload.Id)
            .ShouldBe(
                [leastRecentlyCheckedUpload.Id, uploadWithNeverCheckedFile.Id],
                ignoreOrder: true
            );
    }

    [Test]
    public async Task GetLeastRecentlyCheckedUploadsToCheckAsync_UploadWithDueAndRecentlyCheckedFiles_LoadsAllUploadedFiles()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration),
            fileCheckedAts: [LocalNow.AddHours(-1), LocalNow.AddMinutes(-5)]
        );

        // Act
        var result = await GetLeastRecentlyCheckedUploadsToCheckAsync(registration.Id);

        // Assert
        result.ShouldHaveSingleItem().UploadedFiles.Count.ShouldBe(2);
    }

    [TestCase(-25, true)]
    [TestCase(-24, true)]
    [TestCase(-23, false)]
    [TestCase(null, false)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_NotFullyOnlineSinceRelativeToReleaseGroupHours_ReturnsUploadOnlyWhenWaitingTimeHasPassed(
        int? notFullyOnlineHoursBeforeNow,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: OnlineState.PartiallyOnline,
            notFullyOnlineSince: notFullyOnlineHoursBeforeNow is null
                ? null
                : LocalNow.AddHours(notFullyOnlineHoursBeforeNow.Value)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(2, true)]
    [TestCase(4, false)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_HosterRegistrationHoursOverride_UsesOverrideInsteadOfReleaseGroupHours(
        int numberOfHoursUntilReuploadOverride,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            "Hoster",
            isActive: true,
            numberOfHoursUntilReuploadOverride: numberOfHoursUntilReuploadOverride
        );
        await AddUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: OnlineState.Offline,
            notFullyOnlineSince: LocalNow.AddHours(-3),
            fullyOfflineSince: LocalNow.AddHours(-3)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(null, OnlineState.PartiallyOnline, true)]
    [TestCase(ReuploadTrigger.PartiallyOrFullyOffline, OnlineState.PartiallyOnline, true)]
    [TestCase(ReuploadTrigger.OnlyWhenFullyOffline, OnlineState.PartiallyOnline, false)]
    [TestCase(ReuploadTrigger.OnlyWhenFullyOffline, OnlineState.Offline, true)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_ReuploadTriggerOverride_UsesFullyOfflineSinceOnlyForOnlyWhenFullyOfflineTrigger(
        ReuploadTrigger? reuploadTriggerOverride,
        OnlineState onlineState,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(
            "Hoster",
            isActive: true,
            reuploadTriggerOverride: reuploadTriggerOverride
        );
        await AddUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: onlineState,
            notFullyOnlineSince: LocalNow.AddHours(-30),
            fullyOfflineSince: onlineState is OnlineState.Offline ? LocalNow.AddHours(-30) : null
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_SeveralOfflineUploadsOfSameUploadConfig_ConsidersOnlyLatestOfflineUpload(
        bool latestUploadIsDue
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        var uploadConfig = await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24);
        await AddOfflineUploadAsync(uploadConfig, offlineSince: LocalNow.AddHours(-100));
        var latestUpload = await AddOfflineUploadAsync(
            uploadConfig,
            offlineSince: latestUploadIsDue ? LocalNow.AddHours(-30) : LocalNow.AddHours(-1)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Select(upload => upload.Id).ShouldBe(latestUploadIsDue ? [latestUpload.Id] : []);
    }

    [TestCase(UploadState.Completed, OnlineState.Online, true, false)]
    [TestCase(UploadState.Pending, OnlineState.Unknown, true, false)]
    [TestCase(UploadState.WaitingForArchive, OnlineState.Unknown, true, false)]
    [TestCase(UploadState.Failed, OnlineState.Unknown, false, false)]
    [TestCase(UploadState.Canceled, OnlineState.Unknown, true, false)]
    [TestCase(UploadState.Canceled, OnlineState.Unknown, false, true)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_OtherUploadOfSameUploadConfig_ReturnsUploadOnlyWithoutBlockingUpload(
        UploadState otherUploadState,
        OnlineState otherOnlineState,
        bool otherUploadIsNewer,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        var uploadConfig = await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24);

        if (!otherUploadIsNewer)
        {
            await AddUploadAsync(
                uploadConfig,
                fileCheckedAts: [],
                uploadState: otherUploadState,
                onlineState: otherOnlineState
            );
        }

        await AddOfflineUploadAsync(uploadConfig, offlineSince: LocalNow.AddHours(-30));

        if (otherUploadIsNewer)
        {
            await AddUploadAsync(
                uploadConfig,
                fileCheckedAts: [],
                uploadState: otherUploadState,
                onlineState: otherOnlineState
            );
        }

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [TestCase(true, true, QualityGateState.Passed, true, true)]
    [TestCase(true, true, QualityGateState.NotEvaluated, true, true)]
    [TestCase(true, true, QualityGateState.ManuallyApproved, true, true)]
    [TestCase(true, true, QualityGateState.Failed, true, false)]
    [TestCase(false, true, QualityGateState.Passed, true, false)]
    [TestCase(true, false, QualityGateState.Passed, true, false)]
    [TestCase(true, true, QualityGateState.Passed, false, false)]
    public async Task GetUploadsDueForAutomaticReuploadAsync_DueUpload_ReturnsUploadOnlyWhenReuploadsAreEnabledHosterIsActiveQualityGateDidNotFailAndFilesExist(
        bool enableAutomaticReuploads,
        bool hosterIsActive,
        QualityGateState qualityGateState,
        bool hasUploadedFiles,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: hosterIsActive);
        await AddUploadAsync(
            await AddUploadConfigAsync(
                registration,
                numberOfHoursUntilReupload: 24,
                enableAutomaticReuploads: enableAutomaticReuploads,
                qualityGateState: qualityGateState
            ),
            fileCheckedAts: hasUploadedFiles ? [LocalNow.AddHours(-1)] : [],
            onlineState: OnlineState.Offline,
            notFullyOnlineSince: LocalNow.AddHours(-30),
            fullyOfflineSince: LocalNow.AddHours(-30)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [Test]
    public async Task GetUploadsDueForAutomaticReuploadAsync_MoreDueUploadsThanMaximumCount_ReturnsUploadsWithLowestIds()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        var firstUpload = await AddOfflineUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            offlineSince: LocalNow.AddHours(-30)
        );
        var secondUpload = await AddOfflineUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            offlineSince: LocalNow.AddHours(-30)
        );
        await AddOfflineUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            offlineSince: LocalNow.AddHours(-30)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync(maximumUploadCount: 2);

        // Assert
        result.Select(upload => upload.Id).ShouldBe([firstUpload.Id, secondUpload.Id]);
    }

    [Test]
    public async Task GetUploadsDueForAutomaticReuploadAsync_HosterRegistrationOfUploadInactiveButOfUploadConfigActive_ReturnsNoUpload()
    {
        // Arrange
        var registrationOfUploadConfig = await AddHosterRegistrationAsync(
            "Configured",
            isActive: true
        );
        var registrationOfUpload = await AddHosterRegistrationAsync("Holding", isActive: false);
        await AddUploadAsync(
            await AddUploadConfigAsync(registrationOfUploadConfig, numberOfHoursUntilReupload: 24),
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: OnlineState.Offline,
            notFullyOnlineSince: LocalNow.AddHours(-30),
            fullyOfflineSince: LocalNow.AddHours(-30),
            hosterRegistrationOfUpload: registrationOfUpload
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetUploadsDueForAutomaticReuploadAsync_HoursOverrideOnlyOnHosterRegistrationOfUpload_UsesOverrideOfHosterRegistrationOfUpload()
    {
        // Arrange
        var registrationOfUploadConfig = await AddHosterRegistrationAsync(
            "Configured",
            isActive: true
        );
        var registrationOfUpload = await AddHosterRegistrationAsync(
            "Holding",
            isActive: true,
            numberOfHoursUntilReuploadOverride: 2
        );
        var upload = await AddUploadAsync(
            await AddUploadConfigAsync(registrationOfUploadConfig, numberOfHoursUntilReupload: 24),
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: OnlineState.Offline,
            notFullyOnlineSince: LocalNow.AddHours(-3),
            fullyOfflineSince: LocalNow.AddHours(-3),
            hosterRegistrationOfUpload: registrationOfUpload
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        var dueUpload = result.ShouldHaveSingleItem();
        dueUpload.Id.ShouldBe(upload.Id);
        dueUpload.HosterRegistration!.Name.ShouldBe("Holding");
    }

    [Test]
    public async Task GetUploadForOnlineCheckAsync_UploadOnOtherHosterRegistrationThanItsUploadConfig_LoadsHosterRegistrationOfUpload()
    {
        // Arrange
        var registrationOfUploadConfig = await AddHosterRegistrationAsync(
            "Configured",
            isActive: true
        );
        var registrationOfUpload = await AddHosterRegistrationAsync("Holding", isActive: true);
        var upload = await AddUploadAsync(
            await AddUploadConfigAsync(registrationOfUploadConfig),
            fileCheckedAts: [null],
            hosterRegistrationOfUpload: registrationOfUpload
        );

        // Act
        var result = await repository.GetUploadForOnlineCheckAsync(
            upload.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.HosterRegistration!.Name.ShouldBe("Holding");
        result.UploadedFiles.ShouldHaveSingleItem();
    }

    [Test]
    public async Task GetUploadsDueForAutomaticReuploadAsync_DueUpload_LoadsUploadedFilesHosterRegistrationAndReleaseWithReleaseGroup()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync("Hoster", isActive: true);
        await AddOfflineUploadAsync(
            await AddUploadConfigAsync(registration, numberOfHoursUntilReupload: 24),
            offlineSince: LocalNow.AddHours(-30)
        );

        // Act
        var result = await GetUploadsDueForAutomaticReuploadAsync();

        // Assert
        var upload = result.ShouldHaveSingleItem();
        upload.UploadedFiles.ShouldHaveSingleItem();
        upload.HosterRegistration!.Name.ShouldBe("Hoster");
        upload.UploadConfig.Release.ReleaseGroup.Name.ShouldBe("Release group");
        upload.UploadConfig.Release.MediaFiles.ShouldBeEmpty();
        upload.UploadConfig.Release.QualityIssues.ShouldBeEmpty();
    }

    [Test]
    public async Task GetUploadConfigsWithoutUploadsAsync_HosterRegistrationAndHosterAccountPoolConfigs_ReturnsConfigsWithActiveHosterRegistrationOrPoolMember()
    {
        // Arrange
        var testData = new MaterializedValuesTestData(DbContext);
        var release = testData.AddRelease("Release");
        var activeRegistration = testData.AddHosterRegistration("Active");
        var inactiveRegistration = testData.AddHosterRegistration("Inactive", isActive: false);
        var activeRegistrationConfig = testData.AddUploadConfig(release, activeRegistration);
        testData.AddUploadConfig(release, inactiveRegistration);
        var poolWithActiveMemberConfig = testData.AddHosterAccountPoolUploadConfig(
            release,
            testData.AddHosterAccountPool(
                "WithActiveMember",
                inactiveRegistration,
                activeRegistration
            )
        );
        testData.AddHosterAccountPoolUploadConfig(
            release,
            testData.AddHosterAccountPool("WithoutActiveMember", inactiveRegistration)
        );
        var poolConfigWithUpload = testData.AddHosterAccountPoolUploadConfig(
            release,
            testData.AddHosterAccountPool("WithUpload", activeRegistration)
        );
        testData.AddUnassignedUpload(poolConfigWithUpload, UploadState.WaitingForArchive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUploadConfigsWithoutUploadsAsync(
            MaterializedValuesTestData.LocalNow,
            CancellationToken.None
        );

        // Assert
        result
            .Select(uploadConfig => uploadConfig.Id)
            .ShouldBe(
                [activeRegistrationConfig.Id, poolWithActiveMemberConfig.Id],
                ignoreOrder: true
            );
    }

    private async Task<IReadOnlyList<Upload>> GetLeastRecentlyCheckedUploadsToCheckAsync(
        int hosterRegistrationId,
        int maximumUploadCount = 100
    )
    {
        return await repository.GetLeastRecentlyCheckedUploadsToCheckAsync(
            hosterRegistrationId: hosterRegistrationId,
            localNow: LocalNow,
            maximumUploadCount: maximumUploadCount,
            cancellationToken: CancellationToken.None
        );
    }

    private async Task<IReadOnlyList<Upload>> GetUploadsDueForAutomaticReuploadAsync(
        int maximumUploadCount = 100
    )
    {
        return await repository.GetUploadsDueForAutomaticReuploadAsync(
            localNow: LocalNow,
            maximumUploadCount: maximumUploadCount,
            cancellationToken: CancellationToken.None
        );
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(
        string name,
        bool isActive,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null
    )
    {
        var registration = new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "Hoster",
            IsActive = isActive,
            NumberOfHoursUntilReuploadOverride = numberOfHoursUntilReuploadOverride,
            ReuploadTriggerOverride = reuploadTriggerOverride,
        };

        DbContext.HosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        HosterRegistration hosterRegistration,
        int numberOfHoursUntilReupload = 24,
        bool enableAutomaticReuploads = true,
        QualityGateState qualityGateState = QualityGateState.Passed
    )
    {
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            CreatedAt = LocalNow.AddDays(-10),
            ReleaseType = ReleaseType.Unmanaged,
            QualityGateState = qualityGateState,
            ReleaseGroup = new ReleaseGroup
            {
                Name = "Release group",
                EnableAutomaticReuploads = enableAutomaticReuploads,
                NumberOfHoursUntilReupload = numberOfHoursUntilReupload,
            },
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/archives",
                ArchiverName = "zip",
                ArchiveNamePrefix = "bearcat-release",
                ArchivePassword = "secret",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistrationId = hosterRegistration.Id,
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<Upload> AddOfflineUploadAsync(
        UploadConfig uploadConfig,
        DateTime offlineSince
    )
    {
        return await AddUploadAsync(
            uploadConfig,
            fileCheckedAts: [LocalNow.AddHours(-1)],
            onlineState: OnlineState.Offline,
            notFullyOnlineSince: offlineSince,
            fullyOfflineSince: offlineSince
        );
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        IReadOnlyList<DateTime?> fileCheckedAts,
        UploadState uploadState = UploadState.Completed,
        OnlineState onlineState = OnlineState.Online,
        DateTime? notFullyOnlineSince = null,
        DateTime? fullyOfflineSince = null,
        HosterRegistration? hosterRegistrationOfUpload = null
    )
    {
        var archive = new Archive
        {
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            ArchiveFolderPath = "/archives/bearcat-release",
            ArchiveState = ArchiveState.Created,
            CreatedAt = LocalNow.AddDays(-5),
            ArchiveFiles = fileCheckedAts
                .Select(
                    (_, index) =>
                        new ArchiveFile
                        {
                            FullFileName = $"/archives/bearcat-release.part{index + 1}.rar",
                        }
                )
                .ToList(),
            Uploads = [],
            ErrorMessages = [],
        };
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            HosterRegistrationId =
                hosterRegistrationOfUpload?.Id ?? uploadConfig.HosterRegistrationId,
            Archive = archive,
            CreatedAt = LocalNow.AddDays(-5),
            UploadedAt = LocalNow.AddDays(-5),
            UploadState = uploadState,
            OnlineState = onlineState,
            NotFullyOnlineSince = notFullyOnlineSince,
            FullyOfflineSince = fullyOfflineSince,
            ErrorMessages = [],
            UploadedFiles = fileCheckedAts
                .Select(
                    (checkedAt, index) =>
                        new UploadedFile
                        {
                            ArchiveFile = archive.ArchiveFiles[index],
                            HosterFileLink = $"https://hoster.test/{index + 1}",
                            OnlineState =
                                onlineState is OnlineState.Offline
                                    ? OnlineState.Offline
                                    : OnlineState.Online,
                            CreatedAt = LocalNow.AddDays(-5),
                            CheckedAt = checkedAt,
                            ErrorMessages = [],
                        }
                )
                .ToList(),
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload;
    }
}
