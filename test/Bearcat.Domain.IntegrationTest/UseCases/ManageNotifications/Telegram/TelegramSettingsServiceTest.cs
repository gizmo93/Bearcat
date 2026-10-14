using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications.Telegram;

public class TelegramSettingsServiceTest(DatabaseProvider databaseProvider)
    : TelegramIntegrationTest(databaseProvider)
{
    private TelegramSettingsService service = null!;

    [SetUp]
    public void Setup()
    {
        service = CreateSettingsService();
    }

    [Test]
    public async Task SaveConfigurationAsync_NewBotTokenForUnreadableConfiguration_ClearsFlagAndResolvesNotification()
    {
        // Arrange
        DbContext.TelegramConfigurations.Add(
            CreateConnectedConfiguration(hasUnreadableSecrets: true)
        );
        var notification = new Notification
        {
            CreatedAt = DateTime.UtcNow,
            NotificationSeverity = NotificationSeverity.Error,
            NotificationKind = NotificationKind.UnreadableSecretsDetected,
            Message = "Stored credentials could not be decrypted",
        };
        DbContext.Notifications.Add(notification);
        await DbContext.SaveChangesAsync();

        // Act
        await service.SaveConfigurationAsync(
            "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy",
            "http://bearcat.internal",
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var configuration = await DbContext.TelegramConfigurations.SingleAsync();
        configuration.HasUnreadableSecrets.ShouldBeFalse();
        configuration.EncryptedBotToken.ShouldBe("1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy");
        (
            await DbContext.Notifications.SingleAsync(n => n.Id == notification.Id)
        ).ResolvedAt.ShouldNotBeNull();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetSettingsAsync_ConfigurationExists_ReturnsUnreadableSecretsFlag(
        bool hasUnreadableSecrets
    )
    {
        // Arrange
        DbContext.TelegramConfigurations.Add(CreateConnectedConfiguration(hasUnreadableSecrets));
        await DbContext.SaveChangesAsync();

        // Act
        var settings = await service.GetSettingsAsync(
            "http://bearcat.internal",
            CancellationToken.None
        );

        // Assert
        settings.IsConfigured.ShouldBeTrue();
        settings.HasUnreadableSecrets.ShouldBe(hasUnreadableSecrets);
    }
}
