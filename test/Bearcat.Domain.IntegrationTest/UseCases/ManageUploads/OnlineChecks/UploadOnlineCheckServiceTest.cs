using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.OnlineChecks;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads.OnlineChecks;

public class UploadOnlineCheckServiceTest(DatabaseProvider databaseProvider)
    : UploadIntegrationTest(databaseProvider)
{
    private UploadOnlineCheckService service = null!;

    [SetUp]
    public void SetUpService()
    {
        service = CreateUploadOnlineCheckService();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterReportsAllFilesOnline_KeepsUploadOnline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        upload.UploadedFiles[0].ExternalId = "external-1";
        await DbContext.SaveChangesAsync();

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files =>
                        files.Count == 2
                        && files.Any(file =>
                            file.Url == "https://hoster.test/1" && file.ExternalId == "external-1"
                        )
                    ),
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
                        ["https://hoster.test/2"] = true,
                    }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.CheckedAt > LocalNow.AddMinutes(-1));
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterRegistrationUsesSpecificUploadProxyServer_ChecksLinksInsideUploadProxyScope()
    {
        // Arrange
        var proxyServer = new ProxyServer
        {
            Name = "Upload proxy",
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = 8080,
        };
        DbContext.ProxyServers.Add(proxyServer);
        await DbContext.SaveChangesAsync();
        var hosterRegistration = CreateHosterRegistration();
        hosterRegistration.UploadProxySelection = ProxySelection.SpecificProxyServer;
        hosterRegistration.UploadProxyServerId = proxyServer.Id;
        hosterRegistration.MirrorDownloadProxySelection = ProxySelection.NoProxy;
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        ProxyCategoryScopeState? stateDuringLinkCheck = null;
        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => stateDuringLinkCheck = ProxyCategoryScope.Current)
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool> { ["https://hoster.test/1"] = true }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        stateDuringLinkCheck.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.HosterUploads,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterReportsDownloadCounts_PersistsDownloadCountPerFile()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
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
                        ["https://hoster.test/2"] = true,
                    },
                    new Dictionary<string, int> { ["https://hoster.test/1"] = 150 }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        var firstFile = result.UploadedFiles.Single(f =>
            f.HosterFileLink == "https://hoster.test/1"
        );
        var secondFile = result.UploadedFiles.Single(f =>
            f.HosterFileLink == "https://hoster.test/2"
        );

        firstFile.DownloadCount.ShouldBe(150);
        secondFile.DownloadCount.ShouldBeNull();
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_PreviousUploadSupersededByNewerCompletedUpload_OnlyChecksNewestUpload()
    {
        // Arrange
        var staleCheckedAt = LocalNow.AddHours(-1);
        var supersededUpload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: staleCheckedAt,
            uploadedFileLinks: ["https://hoster.test/old1", "https://hoster.test/old2"]
        );

        var newerArchive = new Archive
        {
            ArchiveConfigId = supersededUpload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = "/tmp/archive",
            ArchiveState = ArchiveState.Created,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile { FullFileName = "new1.rar" },
                new ArchiveFile { FullFileName = "new2.rar" },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var newerUpload = new Upload
        {
            UploadConfigId = supersededUpload.UploadConfigId,
            HosterRegistrationId = supersededUpload.HosterRegistrationId,
            Archive = newerArchive,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = LocalNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = newerArchive.ArchiveFiles[0],
                    HosterFileLink = "https://hoster.test/new1",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = LocalNow.AddHours(-1),
                    CheckedAt = staleCheckedAt,
                },
                new UploadedFile
                {
                    ArchiveFile = newerArchive.ArchiveFiles[1],
                    HosterFileLink = "https://hoster.test/new2",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = LocalNow.AddHours(-1),
                    CheckedAt = staleCheckedAt,
                },
            ],
        };
        DbContext.Uploads.Add(newerUpload);
        await DbContext.SaveChangesAsync();

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files =>
                        files.Count == 2
                        && files.All(file =>
                            file.Url == "https://hoster.test/new1"
                            || file.Url == "https://hoster.test/new2"
                        )
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool>
                    {
                        ["https://hoster.test/new1"] = true,
                        ["https://hoster.test/new2"] = true,
                    }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var superseded = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == supersededUpload.Id);
        var newer = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == newerUpload.Id);

        superseded.UploadedFiles.ShouldAllBe(f => f.CheckedAt == staleCheckedAt);
        newer.UploadedFiles.ShouldAllBe(f => f.CheckedAt > LocalNow.AddMinutes(-1));
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_InactiveHosterRegistration_DoesNotCheckUploadState()
    {
        // Arrange
        var checkedAt = LocalNow.AddHours(-1);
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: checkedAt,
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterIsActive: false
        );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().CheckedAt.ShouldBe(checkedAt);
        HosterFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterReportsSomeFilesOffline_MarksUploadPartiallyOnlineAndCreatesWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
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

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
        result.UploadedFiles.Count(f => f.OnlineState == OnlineState.Offline).ShouldBe(1);
        result.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Warning);
        result.Notifications.Single().Message.ShouldBe("Some files are offline on the hoster");
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_AlreadyPartiallyOnlineRemainsPartiallyOnline_DoesNotCreateWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
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

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
        result.Notifications.ShouldBeEmpty();
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_PartiallyOnlineUploadRecovers_MarksUploadOnlineWithoutWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
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
                        ["https://hoster.test/2"] = true,
                    }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Online);
        result.Notifications.ShouldBeEmpty();
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterCheckFailsAndNoSuccessfulCheckWithinThreshold_CreatesErrorNotificationAndKeepsState()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"]
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
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();
        var registration = await DbContext.HosterRegistrations.SingleAsync();
        var notification = await DbContext.Notifications.SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().OnlineState.ShouldBe(OnlineState.Online);
        notification.NotificationKind.ShouldBe(NotificationKind.HosterStatusCheckFailed);
        notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        notification.HosterRegistrationId.ShouldBe(registration.Id);
        notification.UploadId.ShouldBeNull();
        notification.Message.ShouldBe(
            "Failed to check file existence on hoster registration 'Hoster' for 1 uploads, Error messages: API unavailable"
        );
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterCheckDoesNotFinishWithinTimeout_CreatesErrorNotificationAndKeepsState()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        service.LinkCheckTimeout = TimeSpan.FromMilliseconds(20);
        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    IHosterConfig _,
                    IReadOnlyList<FileUrlToCheckDto> _,
                    CancellationToken cancellationToken
                ) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return new FileExistResult(true, [], new Dictionary<string, bool>());
                }
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();
        var notification = await DbContext.Notifications.SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.Single().OnlineState.ShouldBe(OnlineState.Online);
        notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        notification.Message.ShouldBe(
            "Failed to check file existence on hoster registration 'Hoster' for 1 uploads, Error messages: The link check did not finish within 00:00:00.0200000."
        );
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterCheckFailsButSuccessfulCheckWithinThreshold_DoesNotCreateNotification()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
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
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        HosterMock.VerifyAll();
        HosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_MultipleUploadsOnSameHosterRegistration_ChecksAllLinksInSingleCall()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        var firstUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );
        var secondUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/3"],
            hosterRegistration: hosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files => files.Count == 3),
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
                        ["https://hoster.test/2"] = true,
                        ["https://hoster.test/3"] = false,
                    }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var results = await DbContext.Uploads.ToDictionaryAsync(u => u.Id);

        results[firstUpload.Id].OnlineState.ShouldBe(OnlineState.Online);
        results[secondUpload.Id].OnlineState.ShouldBe(OnlineState.Offline);
        HosterMock.Verify(
            h =>
                h.CheckFilesExistAsync(
                    It.IsAny<IHosterConfig>(),
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_SameHosterWithDifferentRegistrations_ChecksEachRegistrationWithItsOwnConfig()
    {
        // Arrange
        const string otherSerializedHosterConfig = "{\"apiKey\":\"other\"}";
        var otherHosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        HosterMock
            .Setup(h => h.DeserializeHosterConfig(otherSerializedHosterConfig))
            .Returns(otherHosterConfigMock.Object);

        var firstUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var otherHosterRegistration = CreateHosterRegistration();
        otherHosterRegistration.SerializedConfig = otherSerializedHosterConfig;
        var secondUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: otherHosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files =>
                        files.Single().Url == "https://hoster.test/1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool> { ["https://hoster.test/1"] = true }
                )
            );
        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    otherHosterConfigMock.Object,
                    It.Is<IReadOnlyList<FileUrlToCheckDto>>(files =>
                        files.Single().Url == "https://hoster.test/2"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(
                    true,
                    [],
                    new Dictionary<string, bool> { ["https://hoster.test/2"] = false }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var results = await DbContext.Uploads.ToDictionaryAsync(u => u.Id);

        results[firstUpload.Id].OnlineState.ShouldBe(OnlineState.Online);
        results[secondUpload.Id].OnlineState.ShouldBe(OnlineState.Offline);
        HosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_CaptchaRequiredForMultipleUploads_DeactivatesRegistrationWithSingleNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var registration = await DbContext.HosterRegistrations.SingleAsync();
        var notifications = await DbContext
            .Notifications.Where(n =>
                n.NotificationKind == NotificationKind.CaptchaVerificationRequired
            )
            .ToListAsync();

        registration.RequiresCaptchaVerification.ShouldBeTrue();
        registration.IsActive.ShouldBeFalse();
        notifications.Single().Message.ShouldContain("Captcha required");
        (await DbContext.Uploads.ToListAsync()).ShouldAllBe(u =>
            u.OnlineState == OnlineState.Online
        );
    }

    [Test]
    public async Task CheckOnlineStateOfUploadNowAsync_RegistrationAlreadyRequiresCaptcha_DoesNotCreateAnotherNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        hosterRegistration.RequiresCaptchaVerification = true;
        hosterRegistration.IsActive = false;
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));

        // Act
        await service.CheckOnlineStateOfUploadNowAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        HosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterCheckFailsForMultipleUploads_CreatesSingleNotificationForRegistration()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-5),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/3"],
            hosterRegistration: hosterRegistration
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
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var notification = await DbContext.Notifications.SingleAsync();

        notification.NotificationKind.ShouldBe(NotificationKind.HosterStatusCheckFailed);
        notification.HosterRegistrationId.ShouldBe(hosterRegistration.Id);
        notification.UploadId.ShouldBeNull();
        notification.Message.ShouldBe(
            "Failed to check file existence on hoster registration 'Hoster' for 2 uploads, Error messages: API unavailable"
        );
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterCheckFailsInConsecutiveRuns_CreatesSingleNotificationForRegistration()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
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
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );
        DbContext.ChangeTracker.Clear();
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow.AddSeconds(20),
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var notification = await DbContext
            .Notifications.Where(n =>
                n.NotificationKind == NotificationKind.HosterStatusCheckFailed
            )
            .SingleAsync();

        notification.HosterRegistrationId.ShouldBe(hosterRegistration.Id);
        HosterMock.Verify(
            h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterRejectsCredentialsForMultipleUploads_DeactivatesRegistrationWithSingleNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var registration = await DbContext.HosterRegistrations.SingleAsync();
        var notification = await DbContext.Notifications.SingleAsync();

        registration.IsActive.ShouldBeFalse();
        notification.NotificationKind.ShouldBe(NotificationKind.HosterCredentialsRejected);
        notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        notification.HosterRegistrationId.ShouldBe(registration.Id);
        notification.UploadId.ShouldBeNull();
        notification.Message.ShouldBe(
            "Hoster registration 'Hoster' was deactivated because the hoster rejected the credentials: Error: Wrong e-mail or password."
        );
        (await DbContext.Uploads.ToListAsync()).ShouldAllBe(u =>
            u.OnlineState == OnlineState.Online
        );
    }

    [Test]
    public async Task CheckOnlineStateOfUploadNowAsync_HosterRejectsCredentialsOfInactiveRegistration_DoesNotCreateNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        hosterRegistration.IsActive = false;
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );

        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        await service.CheckOnlineStateOfUploadNowAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        (await DbContext.HosterRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        HosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_MoreDueUploadsThanOneRunChecks_ChecksOneHundredLeastRecentlyCheckedUploadsOfRegistration()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        var uploads = new List<Upload>();

        foreach (var uploadNumber in Enumerable.Range(1, 101))
        {
            uploads.Add(
                await AddCompletedUploadAsync(
                    OnlineState.Online,
                    checkedAt: LocalNow.AddHours(-uploadNumber),
                    uploadedFileLinks: [$"https://hoster.test/{uploadNumber}"],
                    hosterRegistration: hosterRegistration
                )
            );
        }

        IReadOnlyList<FileUrlToCheckDto> checkedFiles = [];
        HosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    HosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IHosterConfig _, IReadOnlyList<FileUrlToCheckDto> files, CancellationToken _) =>
                    checkedFiles = files
            )
            .ReturnsAsync(
                (IHosterConfig _, IReadOnlyList<FileUrlToCheckDto> files, CancellationToken _) =>
                    new FileExistResult(true, [], files.ToDictionary(file => file.Url, _ => true))
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        checkedFiles.Count.ShouldBe(100);
        checkedFiles.ShouldNotContain(file => file.Url == "https://hoster.test/1");
        DbContext.ChangeTracker.Clear();
        var mostRecentlyCheckedFile = await DbContext.UploadedFiles.SingleAsync(f =>
            f.UploadId == uploads[0].Id
        );
        mostRecentlyCheckedFile.CheckedAt.ShouldBe(LocalNow.AddHours(-1));
    }

    [Test]
    public async Task CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync_HosterReportsAllFilesOffline_MarksUploadOffline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: LocalNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
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
                        ["https://hoster.test/1"] = false,
                        ["https://hoster.test/2"] = false,
                    }
                )
            );

        // Act
        await service.CheckOnlineStateOfLeastRecentlyCheckedUploadsAsync(
            LocalNow,
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Offline);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
        result.Notifications.Single().Message.ShouldBe("All files are offline on the hoster");
    }
}
