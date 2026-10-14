using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications.Telegram;

public class TelegramDeliveryServiceTest(DatabaseProvider databaseProvider)
    : TelegramIntegrationTest(databaseProvider)
{
    private TelegramDeliveryService service = null!;

    [SetUp]
    public void Setup()
    {
        service = CreateDeliveryService();
    }

    [Test]
    public async Task ProcessDeliveriesAsync_ErrorEnabledAndInfoDisabled_SendsOnlyError()
    {
        DbContext.TelegramConfigurations.Add(
            new TelegramConfiguration
            {
                EncryptedBotToken = "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy",
                BotUsername = "bearcat_bot",
                NotificationBaseUrl = "http://bearcat.internal",
                ChatId = 987654321,
                ChatName = "Gizmo",
                ForwardInfo = false,
                ForwardWarning = false,
                ForwardError = true,
                ForwardNotificationsAfterId = 0,
            }
        );
        DbContext.Notifications.AddRange(
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationSeverity = NotificationSeverity.Info,
                NotificationKind = NotificationKind.UploadCompleted,
                Message = "Upload completed",
            },
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationSeverity = NotificationSeverity.Error,
                NotificationKind = NotificationKind.UploadFailed,
                Message = "Upload failed",
            },
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                ResolvedAt = DateTime.UtcNow,
                NotificationSeverity = NotificationSeverity.Error,
                NotificationKind = NotificationKind.UploadFailed,
                Message = "Already resolved",
            }
        );
        await DbContext.SaveChangesAsync();

        await service.ProcessDeliveriesAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        var delivery = await DbContext
            .TelegramDeliveries.Include(item => item.Notification)
            .SingleAsync();
        delivery.Notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        delivery.DeliveredAt.ShouldNotBeNull();
        TelegramHttpHandler.LastSentMessage.ShouldContain("Upload failed");
        TelegramHttpHandler.LastSentMessage.ShouldContain(
            $"http://bearcat.internal/notifications/{delivery.NotificationId}"
        );
    }

    [Test]
    public async Task ProcessDeliveriesAsync_NotificationLinkedToRelease_IncludesEntityNameInMessage()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            ReleaseGroup = releaseGroup,
            Name = "Awesome.Movie.2026.WEB.H265-ZeroTwo",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
        DbContext.AddRange(releaseGroup, release);
        DbContext.TelegramConfigurations.Add(
            new TelegramConfiguration
            {
                EncryptedBotToken = "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy",
                BotUsername = "bearcat_bot",
                NotificationBaseUrl = "http://bearcat.internal",
                ChatId = 987654321,
                ChatName = "Gizmo",
                ForwardInfo = false,
                ForwardWarning = true,
                ForwardError = false,
                ForwardNotificationsAfterId = 0,
            }
        );
        DbContext.Notifications.Add(
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationSeverity = NotificationSeverity.Warning,
                NotificationKind = NotificationKind.FilesOffline,
                Message = "All files are offline on the hoster",
                Release = release,
            }
        );
        await DbContext.SaveChangesAsync();

        await service.ProcessDeliveriesAsync(CancellationToken.None);

        TelegramHttpHandler.LastSentMessage.ShouldContain("All files are offline on the hoster");
        TelegramHttpHandler.LastSentMessage.ShouldContain(
            "Release: Awesome.Movie.2026.WEB.H265-ZeroTwo"
        );
    }

    [Test]
    public async Task ProcessDeliveriesAsync_BotTokenUnreadable_LeavesDeliveriesPendingWithoutSending()
    {
        // Arrange
        DbContext.TelegramConfigurations.Add(
            CreateConnectedConfiguration(hasUnreadableSecrets: true)
        );
        var notification = new Notification
        {
            CreatedAt = DateTime.UtcNow,
            NotificationSeverity = NotificationSeverity.Error,
            NotificationKind = NotificationKind.UploadFailed,
            Message = "Upload failed",
        };
        DbContext.TelegramDeliveries.Add(
            new TelegramDelivery { Notification = notification, CreatedAt = DateTime.UtcNow }
        );
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessDeliveriesAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var delivery = await DbContext.TelegramDeliveries.SingleAsync();
        delivery.DeliveredAt.ShouldBeNull();
        delivery.AttemptCount.ShouldBe(0);
        TelegramHttpHandler.LastSentMessage.ShouldBeEmpty();
    }
}
