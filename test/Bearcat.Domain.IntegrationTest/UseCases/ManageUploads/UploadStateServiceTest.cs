using System.Linq.Expressions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.QualityGate;
using Bearcat.Domain.Shared.QualityGate.Checks;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
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

public class UploadStateServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string HosterClassName = "TestHoster";
    private const string SerializedHosterConfig = "{\"apiKey\":\"test\"}";

    private Mock<IHoster> hosterMock = null!;
    private Mock<IHosterConfig> hosterConfigMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private DateTime localNow;
    private UploadStateService service = null!;

    [SetUp]
    public void Setup()
    {
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
        await DbContext.SaveChangesAsync();

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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Online);
        result.UploadedFiles.ShouldAllBe(f => f.CheckedAt > localNow.AddMinutes(-1));
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterRegistrationUsesSpecificUploadProxyServer_ChecksLinksInsideUploadProxyScope()
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
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        ProxyCategoryScopeState? stateDuringLinkCheck = null;
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStatesAsync_HosterReportsDownloadCounts_PersistsDownloadCountPerFile()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        await DbContext.SaveChangesAsync();

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_PreviousUploadSupersededByNewerCompletedUpload_OnlyChecksNewestUpload()
    {
        // Arrange
        var staleCheckedAt = localNow.AddHours(-1);
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
            Archive = newerArchive,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = localNow.AddHours(-1),
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
                    CreatedAt = localNow.AddHours(-1),
                    CheckedAt = staleCheckedAt,
                },
                new UploadedFile
                {
                    ArchiveFile = newerArchive.ArchiveFiles[1],
                    HosterFileLink = "https://hoster.test/new2",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = localNow.AddHours(-1),
                    CheckedAt = staleCheckedAt,
                },
            ],
        };
        DbContext.Uploads.Add(newerUpload);
        await DbContext.SaveChangesAsync();

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var superseded = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == supersededUpload.Id);
        var newer = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == newerUpload.Id);

        superseded.UploadedFiles.ShouldAllBe(f => f.CheckedAt == staleCheckedAt);
        newer.UploadedFiles.ShouldAllBe(f => f.CheckedAt > localNow.AddMinutes(-1));
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
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_AlreadyPartiallyOnlineRemainsPartiallyOnline_DoesNotCreateWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
        result.Notifications.ShouldBeEmpty();
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_PartiallyOnlineUploadRecovers_MarksUploadOnlineWithoutWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterCheckFailsAndNoSuccessfulCheckWithinThreshold_CreatesErrorNotificationAndKeepsState()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterCheckDoesNotFinishWithinTimeout_CreatesErrorNotificationAndKeepsState()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        service.LinkCheckTimeout = TimeSpan.FromMilliseconds(20);
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStatesAsync_HosterCheckFailsButSuccessfulCheckWithinThreshold_DoesNotCreateNotification()
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
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync();

        result.Id.ShouldBe(upload.Id);
        result.OnlineState.ShouldBe(OnlineState.Online);
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        hosterMock.VerifyAll();
        hosterFactoryMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_MultipleUploadsOnSameHosterRegistration_ChecksAllLinksInSingleCall()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        var firstUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );
        var secondUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/3"],
            hosterRegistration: hosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var results = await DbContext.Uploads.ToDictionaryAsync(u => u.Id);

        results[firstUpload.Id].OnlineState.ShouldBe(OnlineState.Online);
        results[secondUpload.Id].OnlineState.ShouldBe(OnlineState.Offline);
        hosterMock.Verify(
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
    public async Task CheckUploadStatesAsync_SameHosterWithDifferentRegistrations_ChecksEachRegistrationWithItsOwnConfig()
    {
        // Arrange
        const string otherSerializedHosterConfig = "{\"apiKey\":\"other\"}";
        var otherHosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(otherSerializedHosterConfig))
            .Returns(otherHosterConfigMock.Object);

        var firstUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        var otherHosterRegistration = CreateHosterRegistration();
        otherHosterRegistration.SerializedConfig = otherSerializedHosterConfig;
        var secondUpload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: otherHosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        hosterMock
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var results = await DbContext.Uploads.ToDictionaryAsync(u => u.Id);

        results[firstUpload.Id].OnlineState.ShouldBe(OnlineState.Online);
        results[secondUpload.Id].OnlineState.ShouldBe(OnlineState.Offline);
        hosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_CaptchaRequiredForMultipleUploads_DeactivatesRegistrationWithSingleNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStateNowAsync_RegistrationAlreadyRequiresCaptcha_DoesNotCreateAnotherNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        hosterRegistration.RequiresCaptchaVerification = true;
        hosterRegistration.IsActive = false;
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new CaptchaVerificationRequiredException("Captcha required", 400, 2));

        // Act
        await service.CheckUploadStateNowAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        hosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterCheckFailsForMultipleUploads_CreatesSingleNotificationForRegistration()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-5),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/3"],
            hosterRegistration: hosterRegistration
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStatesAsync_HosterCheckFailsInConsecutiveRuns_CreatesSingleNotificationForRegistration()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileExistResult(false, ["API unavailable"], new Dictionary<string, bool>())
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);
        DbContext.ChangeTracker.Clear();
        await service.CheckUploadStatesAsync(localNow.AddSeconds(20), CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var notification = await DbContext
            .Notifications.Where(n =>
                n.NotificationKind == NotificationKind.HosterStatusCheckFailed
            )
            .SingleAsync();

        notification.HosterRegistrationId.ShouldBe(hosterRegistration.Id);
        hosterMock.Verify(
            h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task CheckUploadStatesAsync_HosterRejectsCredentialsForMultipleUploads_DeactivatesRegistrationWithSingleNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );
        await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/2"],
            hosterRegistration: hosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStateNowAsync_HosterRejectsCredentialsOfInactiveRegistration_DoesNotCreateNotification()
    {
        // Arrange
        var hosterRegistration = CreateHosterRegistration();
        hosterRegistration.IsActive = false;
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-4),
            uploadedFileLinks: ["https://hoster.test/1"],
            hosterRegistration: hosterRegistration
        );

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        await service.CheckUploadStateNowAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Notifications.ToListAsync()).ShouldBeEmpty();
        (await DbContext.HosterRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        hosterMock.VerifyAll();
    }

    [Test]
    public async Task CheckUploadStatesAsync_UploadConfigWithoutUploads_CreatesInitialWaitingForArchiveUpload()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStatesAsync_InactiveHosterRegistrationWithoutUploads_DoesNotCreateInitialUpload()
    {
        // Arrange
        await AddUploadConfigAsync(enableAutomaticReuploads: false, hosterIsActive: false);

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploadExists = await DbContext.Uploads.AnyAsync();

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
        var uploadExists = await DbContext.Uploads.AnyAsync();

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
        var upload = await DbContext.Uploads.SingleAsync();

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
    public async Task CheckUploadStatesAsync_HosterHoursOverrideShorterThanReleaseGroup_CreatesReuploadEarlier()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-2),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            numberOfHoursUntilReuploadOverride: 1
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CheckUploadStatesAsync_OnlyWhenFullyOfflineTriggerAndPartiallyOnline_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            enableAutomaticReuploads: true,
            reuploadTriggerOverride: ReuploadTrigger.OnlyWhenFullyOffline
        );
        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CheckUploadStatesAsync_OnlyWhenFullyOfflineTriggerAndFullyOffline_CreatesReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            reuploadTriggerOverride: ReuploadTrigger.OnlyWhenFullyOffline
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadState.ShouldBe(UploadState.WaitingForArchive);
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
        var uploads = await DbContext.Uploads.ToListAsync();

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
        await DbContext.SaveChangesAsync();

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

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
        DbContext.Uploads.Add(canceledReupload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();

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
        DbContext.ChangeTracker.Clear();
        var reupload = await DbContext.Uploads.SingleAsync(u => u.Id == result);

        reupload.ShouldNotBeNull();
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        reupload.OnlineState.ShouldBe(OnlineState.Unknown);
    }

    [Test]
    public async Task CreateManualReuploadAsync_OnlineUpload_ThrowsInvalidUploadStateException()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow,
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
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
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
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = upload.UploadConfigId,
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
    public async Task SetUploadOfflineAsync_OnlineUpload_MarksUploadAndFilesOfflineAndCreatesWarning()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Online,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Notifications)
            .SingleAsync();

        updated.OnlineState.ShouldBe(OnlineState.Offline);
        updated.NotFullyOnlineSince.ShouldNotBeNull();
        updated.FullyOfflineSince.ShouldNotBeNull();
        updated.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
        updated.UploadedFiles.ShouldAllBe(f => f.CheckedAt != null);
        updated.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Warning);
        updated.Notifications.Single().Message.ShouldBe("Upload manually marked as offline");
    }

    [Test]
    public async Task SetUploadOfflineAsync_PartiallyOnlineUpload_MarksUploadOffline()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Uploads.Include(u => u.UploadedFiles).SingleAsync();

        updated.OnlineState.ShouldBe(OnlineState.Offline);
        updated.UploadedFiles.ShouldAllBe(f => f.OnlineState == OnlineState.Offline);
    }

    [Test]
    public async Task SetUploadOfflineAsync_AlreadyOfflineUpload_ReturnsFalse()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow,
            uploadedFileLinks: ["https://hoster.test/1"]
        );

        // Act
        var result = await service.SetUploadOfflineAsync(upload.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task SetUploadOfflineAsync_UploadDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await service.SetUploadOfflineAsync(-1, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
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
        await DbContext.SaveChangesAsync();

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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.CancelUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.Include(u => u.Notifications).SingleAsync();

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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ResumeUploadAsync_CanceledUploadWithoutArchive_SetsUploadWaitingForArchive()
    {
        // Arrange
        var uploadConfig = await AddUploadConfigAsync(enableAutomaticReuploads: false);
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Canceled,
            OnlineState = OnlineState.Unknown,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

        result.ShouldBeTrue();
        updatedUpload.ArchiveId.ShouldBeNull();
        updatedUpload.UploadState.ShouldBe(UploadState.WaitingForArchive);
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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.ResumeUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updatedUpload = await DbContext.Uploads.SingleAsync();

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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        result.ShouldBeTrue();
        (await DbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeFalse();
        (await DbContext.UploadedFiles.AnyAsync(f => f.UploadId == upload.Id)).ShouldBeFalse();
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
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.DeleteUploadAsync(upload.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        result.ShouldBeFalse();
        (await DbContext.Uploads.AnyAsync(u => u.Id == upload.Id)).ShouldBeTrue();
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadNotFullyOnlineSinceWithinThreshold_DoesNotCreateReupload()
    {
        // Arrange
        await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-1),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();

        uploads.Count.ShouldBe(1);
    }

    [Test]
    public async Task CheckUploadStatesAsync_PartiallyOnlineUploadStillBeingRechecked_CreatesReuploadBasedOnNotFullyOnlineSince()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.PartiallyOnline,
            checkedAt: localNow.AddMinutes(-31),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            enableAutomaticReuploads: true
        );
        upload.NotFullyOnlineSince = localNow.AddHours(-25);
        upload.UploadedFiles[1].OnlineState = OnlineState.Offline;
        await DbContext.SaveChangesAsync();

        hosterMock
            .Setup(h =>
                h.CheckFilesExistAsync(
                    hosterConfigMock.Object,
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .OrderBy(u => u.Id)
            .ToListAsync();
        var recheckedUpload = result.Single(u => u.Id == upload.Id);
        var reupload = result.Single(u => u.Id != upload.Id);

        result.Count.ShouldBe(2);
        recheckedUpload.NotFullyOnlineSince.ShouldBe(localNow.AddHours(-25));
        recheckedUpload.UploadedFiles.ShouldContain(f => f.CheckedAt == localNow);
        reupload.UploadConfigId.ShouldBe(upload.UploadConfigId);
        reupload.UploadState.ShouldBe(UploadState.WaitingForArchive);
    }

    [Test]
    public async Task CheckUploadStatesAsync_QualityGateFails_DoesNotCreateInitialUploadAndMarksFailed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile()
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeFalse();
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityGateEvaluatedAt.ShouldBe(localNow);
        release.QualityIssues.Single().Description.ShouldBe("NFO is missing");
    }

    [Test]
    public async Task CheckUploadStatesAsync_QualityGatePasses_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile(),
            releaseInfo: CreateReleaseInfo(),
            hasNfo: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync();
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
    }

    [Test]
    public async Task CheckUploadStatesAsync_ManuallyApprovedFailingRelease_CreatesUploadAndKeepsApproval()
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
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeTrue();
        var updated = await DbContext.Releases.SingleAsync();
        updated.QualityGateState.ShouldBe(QualityGateState.ManuallyApproved);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadDueButQualityGateFailed_DoesNotCreateReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile()
        );
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState = QualityGateState.Failed;
        await DbContext.SaveChangesAsync();

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        var uploads = await DbContext.Uploads.ToListAsync();
        uploads.Count.ShouldBe(1);
        uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadDueAndQualityGatePasses_CreatesReupload()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile(),
            releaseInfo: CreateReleaseInfo(),
            hasNfo: true
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var uploads = await DbContext.Uploads.OrderBy(u => u.Id).ToListAsync();
        uploads.Count.ShouldBe(2);
        uploads.Single(u => u.Id != upload.Id).UploadConfigId.ShouldBe(upload.UploadConfigId);
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
    }

    [Test]
    public async Task CheckUploadStatesAsync_AutomaticReuploadDueButGateNotYetEvaluatedAndFails_SkipsAndStoresFailure()
    {
        // Arrange
        var upload = await AddCompletedUploadAsync(
            OnlineState.Offline,
            checkedAt: localNow.AddHours(-25),
            uploadedFileLinks: ["https://hoster.test/1"],
            enableAutomaticReuploads: true,
            qualityProfile: CreateRequireNfoProfile()
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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
    public async Task CheckUploadStatesAsync_ReleaseGroupWithoutProfile_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(enableAutomaticReuploads: false);

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeTrue();
        var release = await DbContext.Releases.SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Passed);
        release.QualityGateEvaluatedAt.ShouldBe(localNow);
    }

    [Test]
    public async Task CheckUploadStatesAsync_UnmanagedReleaseWithoutNfo_DoesNotCreateInitialUploadAndMarksFailed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateRequireNfoProfile(),
            releaseType: ReleaseType.Unmanaged
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (await DbContext.Uploads.AnyAsync()).ShouldBeFalse();
        var release = await DbContext.Releases.Include(r => r.QualityIssues).SingleAsync();
        release.QualityGateState.ShouldBe(QualityGateState.Failed);
        release.QualityIssues.Single().Description.ShouldBe("NFO is missing");
    }

    [Test]
    public async Task CheckUploadStatesAsync_UnmanagedReleaseWithOnlyFolderBasedRules_CreatesInitialUploadAndMarksPassed()
    {
        // Arrange
        await AddUploadConfigAsync(
            enableAutomaticReuploads: false,
            qualityProfile: CreateFolderBasedProfile(),
            releaseType: ReleaseType.Unmanaged
        );

        // Act
        await service.CheckUploadStatesAsync(localNow, CancellationToken.None);

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

    private async Task<Upload> AddCompletedUploadAsync(
        OnlineState onlineState,
        DateTime? checkedAt,
        IReadOnlyList<string> uploadedFileLinks,
        bool enableAutomaticReuploads = false,
        bool hosterIsActive = true,
        QualityProfile? qualityProfile = null,
        ReleaseInfo? releaseInfo = null,
        bool hasNfo = false,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null,
        HosterRegistration? hosterRegistration = null
    )
    {
        var uploadConfig = await AddUploadConfigAsync(
            enableAutomaticReuploads,
            hosterIsActive,
            qualityProfile: qualityProfile,
            releaseInfo: releaseInfo,
            hasNfo: hasNfo,
            numberOfHoursUntilReuploadOverride: numberOfHoursUntilReuploadOverride,
            reuploadTriggerOverride: reuploadTriggerOverride,
            hosterRegistration: hosterRegistration
        );
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
            NotFullyOnlineSince = onlineState is OnlineState.PartiallyOnline or OnlineState.Offline
                ? checkedAt
                : null,
            FullyOfflineSince = onlineState is OnlineState.Offline ? checkedAt : null,
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

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        bool enableAutomaticReuploads,
        bool hosterIsActive = true,
        DateTime? releaseCreatedAt = null,
        QualityProfile? qualityProfile = null,
        ReleaseInfo? releaseInfo = null,
        bool hasNfo = false,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null,
        ReleaseType releaseType = ReleaseType.Managed,
        HosterRegistration? hosterRegistration = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = enableAutomaticReuploads,
            NumberOfHoursUntilReupload = 24,
            QualityProfile = qualityProfile,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            CreatedAt = releaseCreatedAt ?? localNow.AddMinutes(-10),
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? "/tmp/release" : null,
            ReleaseGroup = releaseGroup,
            ReleaseInfo = releaseInfo,
            ReleaseNfo = hasNfo
                ? new ReleaseNfo { FileName = "release.nfo", Content = "NFO body" }
                : null,
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
        hosterRegistration ??= new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = SerializedHosterConfig,
            HosterClassName = HosterClassName,
            IsActive = hosterIsActive,
            NumberOfHoursUntilReuploadOverride = numberOfHoursUntilReuploadOverride,
            ReuploadTriggerOverride = reuploadTriggerOverride,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private static HosterRegistration CreateHosterRegistration() =>
        new()
        {
            Name = "Hoster",
            SerializedConfig = SerializedHosterConfig,
            HosterClassName = HosterClassName,
            IsActive = true,
        };

    private UploadStateService CreateService(int initialUploadCooldownMinutes = 5)
    {
        var notificationService = new NotificationService(
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        return new UploadStateService(
            new UploadStateRepository(DbContext),
            hosterFactoryMock.Object,
            CreateTimeProvider(),
            new TestApplicationConfigurationProvider(initialUploadCooldownMinutes),
            notificationService,
            new HosterCaptchaVerificationService(notificationService),
            new HosterCredentialsRejectionService(notificationService),
            Mock.Of<ILogger<UploadStateService>>(),
            NoOpSecretProtector.Instance,
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
