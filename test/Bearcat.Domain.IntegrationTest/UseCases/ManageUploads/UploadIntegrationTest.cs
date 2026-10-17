using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads.OnlineChecks;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public abstract class UploadIntegrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    protected const string HosterClassName = "TestHoster";
    protected const string SerializedHosterConfig = "{\"apiKey\":\"test\"}";

    protected Mock<IHoster> HosterMock { get; private set; } = null!;

    protected Mock<IHosterConfig> HosterConfigMock { get; private set; } = null!;

    protected Mock<IHosterFactory> HosterFactoryMock { get; private set; } = null!;

    protected DateTime LocalNow { get; private set; }

    [SetUp]
    public void SetUpHosterMocksAndLocalNow()
    {
        LocalNow = new DateTime(2026, 5, 17, 12, 0, 0, DateTimeKind.Utc);

        HosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        HosterMock = new Mock<IHoster>(MockBehavior.Strict);
        HosterMock.SetupGet(h => h.Name).Returns(HosterClassName);
        HosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedHosterConfig))
            .Returns(HosterConfigMock.Object);

        HosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        HosterFactoryMock.Setup(f => f.GetByName(HosterClassName)).Returns(HosterMock.Object);
    }

    protected async Task<Upload> AddCompletedUploadAsync(
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
            ArchiveConfigId = uploadConfig.ArchiveConfigId!.Value,
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
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            Archive = archive,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = LocalNow.AddHours(-2),
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
                    CreatedAt = LocalNow.AddHours(-2),
                    CheckedAt = checkedAt,
                }
            );
        }

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    protected async Task<UploadConfig> AddUploadConfigAsync(
        bool enableAutomaticReuploads,
        bool hosterIsActive = true,
        DateTime? releaseCreatedAt = null,
        QualityProfile? qualityProfile = null,
        ReleaseInfo? releaseInfo = null,
        bool hasNfo = false,
        int? numberOfHoursUntilReuploadOverride = null,
        ReuploadTrigger? reuploadTriggerOverride = null,
        ReleaseType releaseType = ReleaseType.Managed,
        HosterRegistration? hosterRegistration = null,
        UploadSource uploadSource = UploadSource.ArchiveFiles
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
            CreatedAt = releaseCreatedAt ?? LocalNow.AddMinutes(-10),
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? "/tmp/release" : null,
            ReleaseGroup = releaseGroup,
            ReleaseInfo = releaseInfo,
            ReleaseNfo = hasNfo
                ? new ReleaseNfo { FileName = "release.nfo", Content = "NFO body" }
                : null,
        };
        var archiveConfig =
            uploadSource is UploadSource.ArchiveFiles
                ? new ArchiveConfig
                {
                    Release = release,
                    Name = "Main archive",
                    ArchiveFilesBasePath = "/tmp/archive",
                    ArchiverName = "zip",
                    ArchiveNamePrefix = "bearcat-release",
                    ArchivePassword = "secret",
                    ArchiveFileSizeMb = 512,
                }
                : null;
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
            UploadSource = uploadSource,
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    protected static HosterRegistration CreateHosterRegistration() =>
        new()
        {
            Name = "Hoster",
            SerializedConfig = SerializedHosterConfig,
            HosterClassName = HosterClassName,
            IsActive = true,
        };

    protected UploadOnlineCheckService CreateUploadOnlineCheckService()
    {
        var notificationService = CreateNotificationService();

        return new UploadOnlineCheckService(
            new UploadStateRepository(DbContext),
            HosterFactoryMock.Object,
            CreateTimeProvider(),
            notificationService,
            new HosterCaptchaVerificationService(notificationService),
            new HosterCredentialsRejectionService(notificationService),
            Mock.Of<ILogger<UploadOnlineCheckService>>(),
            NoOpSecretProtector.Instance
        );
    }

    protected NotificationService CreateNotificationService()
    {
        return new NotificationService(
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );
    }

    protected static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
