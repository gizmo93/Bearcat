using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseRead;

public class ReleaseReadRepositoryUploadTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime MidnightUtc = ReleaseReadRepositoryTestData.MidnightUtc;

    private ReleaseReadRepositoryTestData testData = null!;
    private ReleaseReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        testData = new ReleaseReadRepositoryTestData(DbContext);

        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([new ArchiverDto("RAR", "RarArchiver", ".rar")]);

        var linkCrypterFactory = new Mock<ILinkCrypterFactory>();
        linkCrypterFactory
            .Setup(factory => factory.GetLinkCrypters())
            .Returns([
                new LinkCrypterDto("FileCrypt", "FileCrypt", [], true, true, false),
                new LinkCrypterDto("KeepLinks", "KeepLinks", [], false, true, true),
            ]);

        repository = new ReleaseReadRepository(
            ReadDbContext,
            archiverFactory.Object,
            linkCrypterFactory.Object
        );
    }

    [Test]
    public async Task UploadExistsAsync_UploadOfReleaseAndUploadOfOtherRelease_ReturnsTrueOnlyForOwnUpload()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Own.2026-GRP");
        var ownUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherUpload = testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        await DbContext.SaveChangesAsync();

        // Act
        var ownUploadExists = await repository.UploadExistsAsync(
            release.Id,
            ownUpload.Id,
            CancellationToken.None
        );
        var otherUploadExists = await repository.UploadExistsAsync(
            release.Id,
            otherUpload.Id,
            CancellationToken.None
        );

        // Assert
        ownUploadExists.ShouldBeTrue();
        otherUploadExists.ShouldBeFalse();
    }

    [Test]
    public async Task SearchUploadLinksAsync_OnlineStateFilter_ReturnsMatchingLinksOrderedByFileName()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Links.2026-GRP");
        var uploadConfig = testData.AddUploadConfig(release, "Upload");
        var upload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        testData.AddUploadedFile(
            upload,
            "bearcat.part03.rar",
            "https://hoster.example/part03",
            OnlineState.Offline
        );
        var secondPart = testData.AddUploadedFile(
            upload,
            "bearcat.part02.rar",
            "https://hoster.example/part02"
        );
        secondPart.ErrorMessages = ["Slow response"];
        var firstPart = testData.AddUploadedFile(
            upload,
            "bearcat.part01.rar",
            "https://hoster.example/part01"
        );
        firstPart.CheckedAt = MidnightUtc.AddHours(5).AddMilliseconds(375);
        firstPart.DownloadCount = 42;
        var otherUpload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        testData.AddUploadedFile(otherUpload, "bearcat.part00.rar", "https://hoster.example/other");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchUploadLinksAsync(
            new ReleaseUploadLinkSearchQuery(release.Id, upload.Id, OnlineState.Online),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(link => link.FileName)
            .ShouldBe(["bearcat.part01.rar", "bearcat.part02.rar"]);

        var first = result.Items[0];
        first.HosterFileLink.ShouldBe("https://hoster.example/part01");
        first.OnlineState.ShouldBe(OnlineState.Online);
        first.CheckedAt.ShouldBe(MidnightUtc.AddHours(5).AddMilliseconds(375));
        first.DownloadCount.ShouldBe(42);
        first.ErrorMessages.ShouldBeEmpty();

        var second = result.Items[1];
        second.CheckedAt.ShouldBeNull();
        second.DownloadCount.ShouldBeNull();
        second.ErrorMessages.ShouldBe(["Slow response"]);
    }

    [Test]
    public async Task SearchUploadLinksAsync_SecondPageWithoutOnlineState_ReturnsRemainingLinks()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Paging.2026-GRP");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        foreach (var part in new[] { 4, 7, 1, 6, 3, 2, 5 })
        {
            testData.AddUploadedFile(
                upload,
                $"bearcat.part0{part}.rar",
                $"https://hoster.example/part0{part}",
                part % 2 == 0 ? OnlineState.Offline : OnlineState.Online
            );
        }

        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchUploadLinksAsync(
            new ReleaseUploadLinkSearchQuery(release.Id, upload.Id, PageIndex: 1, PageSize: 5),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(7);
        result
            .Items.Select(link => link.FileName)
            .ShouldBe(["bearcat.part06.rar", "bearcat.part07.rar"]);
    }

    [Test]
    public async Task GetUploadLinksAsync_WithoutOnlineState_ReturnsAllLinksOfUploadOrderedByFileName()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Links.2026-GRP");
        var uploadConfig = testData.AddUploadConfig(release, "Upload");
        var upload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        testData.AddUploadedFile(upload, "bearcat.part02.rar", "https://hoster.example/part02");
        testData.AddUploadedFile(
            upload,
            "bearcat.part01.rar",
            "https://hoster.example/part01",
            OnlineState.Offline
        );
        var otherUpload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        testData.AddUploadedFile(otherUpload, "bearcat.part00.rar", "https://hoster.example/other");
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadLinksAsync(
            release.Id,
            upload.Id,
            cancellationToken: CancellationToken.None
        );
        var resultForOtherRelease = await repository.GetUploadLinksAsync(
            otherRelease.Id,
            upload.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe(["https://hoster.example/part01", "https://hoster.example/part02"]);
        resultForOtherRelease.ShouldBeEmpty();
    }

    [Test]
    public async Task GetUploadLinksAsync_OnlineStateFilter_ReturnsOnlyMatchingLinks()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Links.2026-GRP");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            upload,
            "bearcat.part03.rar",
            "https://hoster.example/part03",
            OnlineState.Offline
        );
        testData.AddUploadedFile(upload, "bearcat.part02.rar", "https://hoster.example/part02");
        testData.AddUploadedFile(
            upload,
            "bearcat.part01.rar",
            "https://hoster.example/part01",
            OnlineState.Offline
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadLinksAsync(
            release.Id,
            upload.Id,
            OnlineState.Offline,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(["https://hoster.example/part01", "https://hoster.example/part03"]);
    }

    [Test]
    public async Task GetUploadContainerLinksAsync_ReleaseAndCollectionContainers_ReturnsBothOrderedByRegistrationNameAndCreatedAt()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Containers.2026-GRP");
        var uploadConfig = testData.AddUploadConfig(release, "Upload");
        var upload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        var otherUpload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        var fileCryptRegistration = testData.AddLinkCrypterRegistration("Crypter B", "FileCrypt");
        var keepLinksRegistration = testData.AddLinkCrypterRegistration("Crypter A", "KeepLinks");

        var laterReleaseContainer = testData.AddReleaseContainer(
            upload,
            fileCryptRegistration,
            "https://crypter.example/later",
            MidnightUtc.AddHours(1)
        );
        var earlierReleaseContainer = testData.AddReleaseContainer(
            upload,
            fileCryptRegistration,
            "https://crypter.example/earlier",
            MidnightUtc.AddMilliseconds(500)
        );
        earlierReleaseContainer.StatusImageId = "status-earlier";
        earlierReleaseContainer.EnableCaptcha = false;
        earlierReleaseContainer.State = LinkCrypterContainerState.CreationFailed;
        earlierReleaseContainer.Errors = ["Captcha required"];
        testData.AddReleaseContainer(
            otherUpload,
            keepLinksRegistration,
            "https://crypter.example/other-upload",
            MidnightUtc
        );
        var collectionContainer = testData.AddCollectionContainer(
            [upload, otherUpload],
            keepLinksRegistration,
            "https://crypter.example/collection",
            MidnightUtc.AddDays(1)
        );
        collectionContainer.EnableClickAndLoad = false;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadContainerLinksAsync(
            release.Id,
            upload.Id,
            CancellationToken.None
        );

        // Assert
        result
            .Select(container => container.Id)
            .ShouldBe([
                collectionContainer.Id,
                earlierReleaseContainer.Id,
                laterReleaseContainer.Id,
            ]);

        var collection = result[0];
        collection.LinkCrypterRegistrationName.ShouldBe("Crypter A");
        collection.LinkCrypterClassName.ShouldBe("KeepLinks");
        collection.ContainerUrl.ShouldBe("https://crypter.example/collection");
        collection.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        collection.CreatedAt.ShouldBe(MidnightUtc.AddDays(1));
        collection.EnableCaptcha.ShouldBeTrue();
        collection.EnableClickAndLoad.ShouldBeFalse();
        collection.SupportsCaptcha.ShouldBeFalse();
        collection.SupportsClickAndLoad.ShouldBeTrue();

        var earlier = result[1];
        earlier.LinkCrypterRegistrationName.ShouldBe("Crypter B");
        earlier.Scope.ShouldBe(LinkCrypterContainerScope.Release);
        earlier.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        earlier.StatusImageId.ShouldBe("status-earlier");
        earlier.CreatedAt.ShouldBe(MidnightUtc.AddMilliseconds(500));
        earlier.EnableCaptcha.ShouldBeFalse();
        earlier.EnableContainerDownload.ShouldBeTrue();
        earlier.SupportsCaptcha.ShouldBeTrue();
        earlier.SupportsContainerDownload.ShouldBeTrue();
        earlier.SupportsClickAndLoad.ShouldBeFalse();
        earlier.Errors.ShouldBe(["Captcha required"]);

        result[2].ContainerUrl.ShouldBe("https://crypter.example/later");
    }

    [Test]
    public async Task SearchUploadsAsync_UploadConfigIdFilter_ReturnsUploadsOfThatConfigOrderedByLatestTimestamp()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Uploads.2026-GRP");
        var searchedConfig = testData.AddUploadConfig(release, "Searched");
        var uploadedLater = testData.AddUpload(
            searchedConfig,
            MidnightUtc,
            MidnightUtc.AddHours(3).AddMilliseconds(1)
        );
        var pendingUpload = testData.AddUpload(
            searchedConfig,
            MidnightUtc.AddHours(3),
            null,
            UploadState.Pending,
            OnlineState.Unknown
        );
        var uploadedEarlier = testData.AddUpload(
            searchedConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(2)
        );
        testData.AddUpload(
            testData.AddUploadConfig(release, "Other"),
            MidnightUtc.AddDays(1),
            MidnightUtc.AddDays(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new ReleaseUploadSearchQuery(release.Id, searchedConfig.Id),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(upload => upload.UploadId)
            .ShouldBe([uploadedLater.Id, pendingUpload.Id, uploadedEarlier.Id]);
        result.Items.ShouldAllBe(upload => upload.UploadConfigName == "Searched");
        result.Items[0].UploadedAt.ShouldBe(MidnightUtc.AddHours(3).AddMilliseconds(1));
        result.Items[1].UploadedAt.ShouldBeNull();
    }

    [Test]
    public async Task SearchUploadsAsync_UploadWithReleaseAndCollectionContainers_CountsLinksAndBothContainers()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Counts.2026-GRP");
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(upload, "bearcat.part01.rar", "https://hoster.example/part01");
        testData.AddUploadedFile(upload, "bearcat.part02.rar", "https://hoster.example/part02");
        var linkCrypterRegistration = testData.AddLinkCrypterRegistration("Crypter");
        testData.AddReleaseContainer(
            upload,
            linkCrypterRegistration,
            "https://crypter.example/release",
            MidnightUtc
        );
        testData.AddCollectionContainer(
            [upload],
            linkCrypterRegistration,
            "https://crypter.example/collection",
            MidnightUtc
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new ReleaseUploadSearchQuery(release.Id),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.ShouldHaveSingleItem();
        item.LinkCount.ShouldBe(2);
        item.ContainerCount.ShouldBe(2);
    }

    [Test]
    public async Task SearchUploadsAsync_OfflineAndFailedUploads_AllowsReuploadOnlyWithoutBlockingSiblingUpload()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Reupload.2026-GRP");
        var singleOfflineUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Single offline"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );

        var blockedByPendingConfig = testData.AddUploadConfig(release, "Blocked by pending");
        var offlineBlockedByPending = testData.AddUpload(
            blockedByPendingConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.PartiallyOnline
        );
        var pendingUpload = testData.AddUpload(
            blockedByPendingConfig,
            MidnightUtc.AddHours(1),
            null,
            UploadState.Pending,
            OnlineState.Unknown
        );

        var blockedByOnlineConfig = testData.AddUploadConfig(release, "Blocked by online");
        var failedBlockedByOnline = testData.AddUpload(
            blockedByOnlineConfig,
            MidnightUtc.AddHours(1),
            null,
            UploadState.Failed,
            OnlineState.Unknown
        );
        var onlineUpload = testData.AddUpload(blockedByOnlineConfig, MidnightUtc, MidnightUtc);

        var notBlockedConfig = testData.AddUploadConfig(release, "Not blocked");
        var canceledUpload = testData.AddUpload(
            notBlockedConfig,
            MidnightUtc.AddHours(1),
            null,
            UploadState.Canceled,
            OnlineState.Unknown
        );
        var offlineNextToCanceled = testData.AddUpload(
            notBlockedConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new ReleaseUploadSearchQuery(release.Id),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(7);
        var canCreateReuploadById = result.Items.ToDictionary(
            upload => upload.UploadId,
            upload => upload.CanCreateReupload
        );
        canCreateReuploadById[singleOfflineUpload.Id].ShouldBeTrue();
        canCreateReuploadById[offlineBlockedByPending.Id].ShouldBeFalse();
        canCreateReuploadById[pendingUpload.Id].ShouldBeFalse();
        canCreateReuploadById[failedBlockedByOnline.Id].ShouldBeFalse();
        canCreateReuploadById[onlineUpload.Id].ShouldBeFalse();
        canCreateReuploadById[canceledUpload.Id].ShouldBeTrue();
        canCreateReuploadById[offlineNextToCanceled.Id].ShouldBeTrue();
    }

    [Test]
    public async Task GetForumPostUploadsAsync_LatestUploadWithFilesAndContainers_ReturnsLinksAndContainerPerLinkCrypter()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.ForumPost.2026-GRP");
        var archiveConfig = testData.AddArchiveConfig(
            release,
            "Archive",
            archivePassword: "archive-secret"
        );
        var releaseCrypterRegistration = testData.AddLinkCrypterRegistration("Crypter Release");
        var collectionCrypterRegistration = testData.AddLinkCrypterRegistration(
            "Crypter Collection",
            "KeepLinks"
        );

        var firstConfig = testData.AddUploadConfig(
            release,
            "Config A",
            testData.AddHosterRegistration("Hoster A"),
            archiveConfig
        );
        testData.AddUploadConfigLinkCrypter(
            firstConfig,
            releaseCrypterRegistration,
            password: "crypter-secret"
        );
        testData.AddUploadConfigLinkCrypter(
            firstConfig,
            collectionCrypterRegistration,
            LinkCrypterContainerScope.ReleaseCollection
        );
        var olderUpload = testData.AddUpload(firstConfig, MidnightUtc, MidnightUtc);
        testData.AddUploadedFile(
            olderUpload,
            "bearcat.old.part01.rar",
            "https://hoster.example/old"
        );
        testData.AddReleaseContainer(
            olderUpload,
            releaseCrypterRegistration,
            "https://crypter.example/old",
            MidnightUtc
        );
        var latestUpload = testData.AddUpload(
            firstConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1).AddMilliseconds(250)
        );
        testData.AddUploadedFile(
            latestUpload,
            "bearcat.part02.rar",
            "https://hoster.example/part02"
        );
        testData.AddUploadedFile(
            latestUpload,
            "bearcat.part01.rar",
            "https://hoster.example/part01"
        );
        var releaseContainer = testData.AddReleaseContainer(
            latestUpload,
            releaseCrypterRegistration,
            "https://crypter.example/release",
            MidnightUtc.AddHours(2)
        );
        releaseContainer.StatusImageId = "status-release";
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherReleaseUpload = testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Config A"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddCollectionContainer(
            [latestUpload, otherReleaseUpload],
            collectionCrypterRegistration,
            "https://crypter.example/collection",
            MidnightUtc.AddHours(3).AddMilliseconds(5)
        );

        var secondConfig = testData.AddUploadConfig(
            release,
            "Config B",
            testData.AddHosterRegistration("Hoster B"),
            archiveConfig
        );
        testData.AddUploadConfigLinkCrypter(secondConfig, releaseCrypterRegistration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetForumPostUploadsAsync(release.Id, CancellationToken.None);

        // Assert
        result.Select(upload => upload.UploadConfigName).ShouldBe(["Config A", "Config B"]);

        var first = result[0];
        first.HosterName.ShouldBe("Hoster A");
        first.ArchiveFormat.ShouldBe("RAR");
        first.ArchivePassword.ShouldBe("archive-secret");
        first.UploadedAt.ShouldBe(MidnightUtc.AddHours(1).AddMilliseconds(250));
        first.Links.ShouldBe(["https://hoster.example/part01", "https://hoster.example/part02"]);
        first.LinkCrypters.Count.ShouldBe(2);
        var releaseCrypter = first.LinkCrypters.Single(crypter =>
            crypter.Name == "Crypter Release"
        );
        releaseCrypter.Password.ShouldBe("crypter-secret");
        releaseCrypter.ContainerUrl.ShouldBe("https://crypter.example/release");
        releaseCrypter.StatusImageId.ShouldBe("status-release");
        releaseCrypter.CreatedAt.ShouldBe(MidnightUtc.AddHours(2));
        var collectionCrypter = first.LinkCrypters.Single(crypter =>
            crypter.Name == "Crypter Collection"
        );
        collectionCrypter.Password.ShouldBeNull();
        collectionCrypter.ContainerUrl.ShouldBe("https://crypter.example/collection");
        collectionCrypter.CreatedAt.ShouldBe(MidnightUtc.AddHours(3).AddMilliseconds(5));

        var second = result[1];
        second.HosterName.ShouldBe("Hoster B");
        second.UploadedAt.ShouldBeNull();
        second.Links.ShouldBeEmpty();
        var unusedCrypter = second.LinkCrypters.ShouldHaveSingleItem();
        unusedCrypter.Name.ShouldBe("Crypter Release");
        unusedCrypter.ContainerUrl.ShouldBeEmpty();
        unusedCrypter.StatusImageId.ShouldBeNull();
    }

    [Test]
    public async Task GetPostQueueAsync_LatestUploadsWithReleaseContainers_CountsContainersPerRegistration()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.PostQueue.2026-GRP");
        var archiveConfig = testData.AddArchiveConfig(release, "Archive");
        var sharedCrypterRegistration = testData.AddLinkCrypterRegistration("Crypter A");
        var singleCrypterRegistration = testData.AddLinkCrypterRegistration("Crypter B");

        var firstConfig = testData.AddUploadConfig(
            release,
            "Config A",
            testData.AddHosterRegistration("Hoster A"),
            archiveConfig
        );
        var firstConfigSharedCrypter = testData.AddUploadConfigLinkCrypter(
            firstConfig,
            sharedCrypterRegistration
        );
        var firstConfigSingleCrypter = testData.AddUploadConfigLinkCrypter(
            firstConfig,
            singleCrypterRegistration
        );
        var olderUpload = testData.AddUpload(firstConfig, MidnightUtc, MidnightUtc);
        testData.AddReleaseContainer(
            olderUpload,
            sharedCrypterRegistration,
            "https://crypter.example/old",
            MidnightUtc,
            firstConfigSharedCrypter
        );
        var firstLatestUpload = testData.AddUpload(
            firstConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1).AddMilliseconds(500)
        );
        testData.AddUploadedFile(
            firstLatestUpload,
            "bearcat.a.part01.rar",
            "https://hoster.example/a1"
        );
        testData.AddUploadedFile(
            firstLatestUpload,
            "bearcat.a.part02.rar",
            "https://hoster.example/a2"
        );
        testData.AddReleaseContainer(
            firstLatestUpload,
            sharedCrypterRegistration,
            "https://crypter.example/a-shared",
            MidnightUtc.AddHours(2),
            firstConfigSharedCrypter
        );
        testData.AddReleaseContainer(
            firstLatestUpload,
            singleCrypterRegistration,
            "https://crypter.example/a-single",
            MidnightUtc.AddHours(2),
            firstConfigSingleCrypter
        );

        var secondConfig = testData.AddUploadConfig(
            release,
            "Config B",
            testData.AddHosterRegistration("Hoster B"),
            archiveConfig
        );
        var secondConfigSharedCrypter = testData.AddUploadConfigLinkCrypter(
            secondConfig,
            sharedCrypterRegistration
        );
        var secondLatestUpload = testData.AddUpload(
            secondConfig,
            MidnightUtc.AddMinutes(30),
            MidnightUtc.AddMinutes(30)
        );
        testData.AddUploadedFile(
            secondLatestUpload,
            "bearcat.b.part01.rar",
            "https://hoster.example/b1"
        );
        testData.AddReleaseContainer(
            secondLatestUpload,
            sharedCrypterRegistration,
            "https://crypter.example/b-shared",
            MidnightUtc.AddHours(2),
            secondConfigSharedCrypter
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);

        // Assert
        var item = result.ShouldHaveSingleItem();
        item.ReleaseId.ShouldBe(release.Id);
        item.LatestUploadedAt.ShouldBe(MidnightUtc.AddHours(1).AddMilliseconds(500));
        var archiveGroup = item.ArchiveGroups.ShouldHaveSingleItem();
        archiveGroup.ArchiveConfigName.ShouldBe("Archive");
        archiveGroup
            .Hosters.Select(hoster => (hoster.HosterRegistrationName, hoster.LinkCount))
            .ShouldBe([("Hoster A", 2), ("Hoster B", 1)]);
        item.Containers.Select(container =>
                (container.LinkCrypterRegistrationName, container.Count)
            )
            .ShouldBe([("Crypter A", 2), ("Crypter B", 1)]);
    }
}
