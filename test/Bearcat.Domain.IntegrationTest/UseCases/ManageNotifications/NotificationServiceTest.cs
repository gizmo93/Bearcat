using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            new NotificationRepository(dbContext),
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateInfoAsync_MessageProvided_PersistsInfoNotification()
    {
        // Arrange
        var message = "Upload completed";

        // Act
        await service.CreateInfoAsync(message, CancellationToken.None);

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationType.ShouldBe(NotificationType.Info);
        result.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
        result.ResolvedAt.ShouldBeNull();
    }

    [Test]
    public async Task CreateWarningAsync_MessageProvided_PersistsWarningNotification()
    {
        // Arrange
        var message = "Some files are offline";

        // Act
        await service.CreateWarningAsync(message, CancellationToken.None);

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationType.ShouldBe(NotificationType.Warning);
    }

    [Test]
    public async Task CreateErrorAsync_MessageProvided_PersistsErrorNotification()
    {
        // Arrange
        var message = "Upload failed";

        // Act
        await service.CreateErrorAsync(message, CancellationToken.None);

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe(message);
        result.NotificationType.ShouldBe(NotificationType.Error);
    }

    [Test]
    public async Task CreateError_EntityProvided_AttachesNotificationToEntity()
    {
        // Arrange
        var upload = await AddUploadAsync();

        // Act
        service.CreateError("Upload failed", upload, n => n.Upload);
        await dbContext.SaveChangesAsync();

        // Assert
        var result = await dbContext.Notifications.SingleAsync();

        result.ShouldNotBeNull();
        result.Message.ShouldBe("Upload failed");
        result.NotificationType.ShouldBe(NotificationType.Error);
        result.UploadId.ShouldBe(upload.Id);
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
            NotificationType = NotificationType.Info,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        return notification;
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
