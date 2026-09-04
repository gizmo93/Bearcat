using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications;

public class NotificationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private NotificationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        service = new NotificationService(
            repository: new NotificationRepository(dbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateConfigurationProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_MessageProvided_PersistsNotificationWithDefinitionSeverity()
    {
        // Arrange
        var message = "Upload completed";

        // Act
        await service.CreateAsync(
            kind: NotificationKind.UploadCompleted,
            message: message,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationKind.ShouldBe(NotificationKind.UploadCompleted);
        result.NotificationSeverity.ShouldBe(NotificationSeverity.Info);
        result.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
        result.ResolvedAt.ShouldBeNull();
    }

    [Test]
    public async Task CreateAsync_MessageProvided_PersistsConfiguredSeverity()
    {
        // Arrange
        var message = "Some files are offline";

        // Act
        await service.CreateAsync(
            kind: NotificationKind.FilesOffline,
            message: message,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationSeverity.ShouldBe(NotificationSeverity.Warning);
    }

    [Test]
    public async Task CreateAsync_MessageProvided_PersistsErrorNotification()
    {
        // Arrange
        var message = "Upload failed";

        // Act
        await service.CreateAsync(
            kind: NotificationKind.UploadFailed,
            message: message,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
    }

    [Test]
    public async Task CreateAsync_DisabledKind_DoesNotPersistNotificationButSavesPendingChanges()
    {
        var configuration = new NotificationConfiguration { CaptchaVerificationRequired = false };
        var pendingRelease = new Release
        {
            Name = "Saved release",
            ReleaseType = ReleaseType.Managed,
            ReleaseGroup = new ReleaseGroup { Name = "Saved group" },
        };
        dbContext.Releases.Add(pendingRelease);
        var disabledService = new NotificationService(
            repository: new NotificationRepository(dbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateConfigurationProvider(configuration)
        );

        await disabledService.CreateAsync(
            kind: NotificationKind.CaptchaVerificationRequired,
            message: "CAPTCHA required",
            cancellationToken: CancellationToken.None
        );

        (await dbContext.Notifications.CountAsync()).ShouldBe(0);
        pendingRelease.Id.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Create_EntityProvided_AttachesNotificationToEntity()
    {
        // Arrange
        var upload = await AddUploadAsync();

        // Act
        service.Create(
            kind: NotificationKind.UploadFailed,
            message: "Upload failed",
            entity: upload,
            selector: n => n.Upload
        );
        await dbContext.SaveChangesAsync();

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe("Upload failed");
        result.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        result.UploadId.ShouldBe(upload.Id);
    }

    [Test]
    public async Task CreateAsync_AllDefinedKinds_RespectSettingsAndDoNotReplayEvents()
    {
        var definitions = NotificationDefinitions.All;
        var definedKinds = definitions.Select(definition => definition.Kind).ToList();
        var expectedKinds = Enum.GetValues<NotificationKind>()
            .Where(kind => kind != NotificationKind.Legacy)
            .ToList();

        definedKinds.Count.ShouldBe(expectedKinds.Count);
        definedKinds.Distinct().Order().ShouldBe(expectedKinds.Order());
        definitions.ShouldAllBe(definition => (int)definition.Group > 0);
        definitions.ShouldAllBe(definition => (int)definition.Severity > 0);

        foreach (var definition in definitions)
        {
            var configuration = new NotificationConfiguration();
            var configuredService = new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateConfigurationProvider(configuration)
            );

            await configuredService.CreateAsync(
                kind: definition.Kind,
                message: definition.Kind.ToString(),
                cancellationToken: CancellationToken.None
            );

            var created = await dbContext.Notifications.SingleAsync(notification =>
                notification.NotificationKind == definition.Kind
            );
            created.NotificationSeverity.ShouldBe(definition.Severity);

            SetEnabled(configuration, definition.Kind, false);
            await configuredService.CreateAsync(
                kind: definition.Kind,
                message: "disabled",
                cancellationToken: CancellationToken.None
            );
            (
                await dbContext.Notifications.CountAsync(notification =>
                    notification.NotificationKind == definition.Kind
                )
            ).ShouldBe(1);

            SetEnabled(configuration, definition.Kind, true);
            await configuredService.CreateAsync(
                kind: definition.Kind,
                message: "enabled again",
                cancellationToken: CancellationToken.None
            );
            (
                await dbContext.Notifications.CountAsync(notification =>
                    notification.NotificationKind == definition.Kind
                )
            ).ShouldBe(2);
        }
    }

    [Test]
    public async Task Create_DisabledKind_DoesNotAddNotificationOrSaveEntity()
    {
        var upload = await AddUploadAsync();
        var configuration = new NotificationConfiguration { UploadFailed = false };
        var configuredService = new NotificationService(
            repository: new NotificationRepository(dbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateConfigurationProvider(configuration)
        );
        upload.UploadState = UploadState.Failed;

        configuredService.Create(
            kind: NotificationKind.UploadFailed,
            message: "disabled",
            entity: upload,
            selector: notification => notification.Upload
        );

        dbContext.ChangeTracker.Entries<Notification>().ShouldBeEmpty();
        dbContext.ChangeTracker.Entries<Upload>().Single().State.ShouldBe(EntityState.Modified);
        dbContext.ChangeTracker.Clear();
        (
            await dbContext.Uploads.SingleAsync(entity => entity.Id == upload.Id)
        ).UploadState.ShouldBe(UploadState.Completed);
    }

    [Test]
    public async Task MarkRequiredAsync_DisabledNotification_PersistsCaptchaStateWithoutNotification()
    {
        var registration = new HosterRegistration
        {
            Name = "Captcha hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        dbContext.HosterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        var configuration = new NotificationConfiguration { CaptchaVerificationRequired = false };
        var configuredService = new NotificationService(
            repository: new NotificationRepository(dbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateConfigurationProvider(configuration)
        );
        var captchaService = new HosterCaptchaVerificationService(configuredService);

        await captchaService.MarkRequiredAsync(
            registration,
            "verification needed",
            CancellationToken.None
        );

        await using var freshContext = Database.CreateDbContext();
        var persisted = await freshContext.HosterRegistrations.SingleAsync(entity =>
            entity.Id == registration.Id
        );
        persisted.RequiresCaptchaVerification.ShouldBeTrue();
        persisted.IsActive.ShouldBeFalse();
        (await freshContext.Notifications.CountAsync()).ShouldBe(0);
    }

    [TestCase(NotificationKind.Legacy)]
    [TestCase((NotificationKind)0)]
    [TestCase((NotificationKind)999)]
    public async Task CreateMethods_InvalidKinds_Throw(NotificationKind kind)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(kind, "invalid", CancellationToken.None)
        );

        var upload = await AddUploadAsync();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            service.Create(kind, "invalid", upload, notification => notification.Upload)
        );
    }

    [Test]
    public async Task ResolveAsync_NotificationExists_SetsResolvedAt()
    {
        // Arrange
        var notification = await AddNotificationAsync("Resolve me");

        // Act
        await service.ResolveAsync(notification.Id, CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(notification.Id);
        result.ResolvedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ResolveAllAsync_OpenNotificationsExist_SetsResolvedAtForAll()
    {
        // Arrange
        await AddNotificationAsync("First");
        await AddNotificationAsync("Second");

        // Act
        await service.ResolveAllAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Notifications.ToListAsync();

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(n => n.ResolvedAt.HasValue);
    }

    private async Task<Notification> AddNotificationAsync(string message)
    {
        var notification = new Notification
        {
            Message = message,
            NotificationKind = NotificationKind.Legacy,
            NotificationSeverity = NotificationSeverity.Info,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        return notification;
    }

    private static IApplicationConfigurationProvider CreateConfigurationProvider(
        NotificationConfiguration? configuration = null
    )
    {
        var provider = new Mock<IApplicationConfigurationProvider>();
        provider
            .Setup(p => p.GetConfiguration<NotificationConfiguration>())
            .Returns(configuration ?? new NotificationConfiguration());

        return provider.Object;
    }

    private static void SetEnabled(
        NotificationConfiguration configuration,
        NotificationKind kind,
        bool value
    )
    {
        typeof(NotificationConfiguration)
            .GetProperty(kind.ToString())!
            .SetValue(configuration, value);
    }

    private async Task<Upload> AddUploadAsync()
    {
        var uploadConfig = await AddUploadConfigAsync();
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
        };

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

        return upload;
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
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
