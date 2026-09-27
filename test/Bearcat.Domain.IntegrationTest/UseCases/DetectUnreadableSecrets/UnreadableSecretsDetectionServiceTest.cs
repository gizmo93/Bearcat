using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.DetectUnreadableSecrets;

public class UnreadableSecretsDetectionServiceTest : BearcatIntegrationTest
{
    private const string PlaintextConfig = "{\"ApiKey\":\"plain\"}";

    private readonly AesGcmSecretProtector currentKeyProtector = new(
        new FixedKeyProvider(fillValue: 1)
    );

    private readonly AesGcmSecretProtector otherKeyProtector = new(
        new FixedKeyProvider(fillValue: 7)
    );

    [Test]
    public async Task DetectAsync_SecretsOfAllTypesEncryptedWithOtherKey_FlagsAllAndCreatesOneGroupedNotification()
    {
        // Arrange
        var unreadableValue = otherKeyProtector.Protect("secret");
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.HosterRegistrations.AddRange(
                CreateHosterRegistration("Hoster B", unreadableValue),
                CreateHosterRegistration("Hoster A", unreadableValue)
            );
            arrangeContext.LinkCrypterRegistrations.Add(
                new LinkCrypterRegistration
                {
                    Name = "Crypter",
                    LinkCrypterClassName = "FileCrypt",
                    SerializedConfig = unreadableValue,
                }
            );
            arrangeContext.ImageHosterRegistrations.Add(
                new ImageHosterRegistration
                {
                    Name = "Images",
                    ImageHosterClassName = "ImgBb",
                    SerializedConfig = unreadableValue,
                }
            );
            arrangeContext.DistributionSiteRegistrations.Add(
                CreateDistributionSiteRegistration("Forum", unreadableValue, session: null)
            );
            arrangeContext.RemoteSourceRegistrations.Add(
                new RemoteSourceRegistration
                {
                    Name = "Seedbox",
                    SourceClassName = "Ftp",
                    SerializedConfig = unreadableValue,
                }
            );
            arrangeContext.NfoDatabaseRegistrations.Add(
                new NfoDatabaseRegistration
                {
                    NfoDatabaseClassName = "srrDB",
                    SerializedConfig = unreadableValue,
                }
            );
            arrangeContext.MediaDatabaseRegistrations.Add(
                new MediaDatabaseRegistration
                {
                    MediaDatabaseClassName = "Tmdb",
                    SerializedConfig = unreadableValue,
                }
            );
            arrangeContext.TelegramConfigurations.Add(CreateTelegramConfiguration(unreadableValue));
            await arrangeContext.SaveChangesAsync();
        }

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (
            await assertContext.HosterRegistrations.AllAsync(r => r.HasUnreadableSecrets)
        ).ShouldBeTrue();
        (
            await assertContext.LinkCrypterRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.ImageHosterRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.DistributionSiteRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.RemoteSourceRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.NfoDatabaseRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.MediaDatabaseRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        (
            await assertContext.TelegramConfigurations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();

        var notification = await assertContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.UnreadableSecretsDetected);
        notification.NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        notification.Message.ShouldBe(
            "Stored credentials could not be decrypted with the current encryption key and must be entered again: "
                + "Hoster: Hoster A, Hoster B; Link crypter: Crypter; Image hoster: Images; "
                + "Distribution site: Forum; Remote source: Seedbox; NFO database: srrDB; "
                + "Media database: Tmdb; Telegram bot token"
        );
    }

    [Test]
    public async Task DetectAsync_ActiveRegistrationsWithUnreadableSecrets_DeactivatesThem()
    {
        // Arrange
        var unreadableValue = otherKeyProtector.Protect("secret");
        await using (var arrangeContext = Database.CreateDbContext())
        {
            var hosterRegistration = CreateHosterRegistration("Hoster", unreadableValue);
            hosterRegistration.IsActive = true;
            arrangeContext.HosterRegistrations.Add(hosterRegistration);
            arrangeContext.LinkCrypterRegistrations.Add(
                new LinkCrypterRegistration
                {
                    Name = "Crypter",
                    LinkCrypterClassName = "FileCrypt",
                    SerializedConfig = unreadableValue,
                    IsActive = true,
                }
            );
            arrangeContext.ImageHosterRegistrations.Add(
                new ImageHosterRegistration
                {
                    Name = "Images",
                    ImageHosterClassName = "ImgBb",
                    SerializedConfig = unreadableValue,
                    IsActive = true,
                }
            );
            var distributionSiteRegistration = CreateDistributionSiteRegistration(
                "Forum",
                unreadableValue,
                session: null
            );
            distributionSiteRegistration.IsActive = true;
            arrangeContext.DistributionSiteRegistrations.Add(distributionSiteRegistration);
            arrangeContext.RemoteSourceRegistrations.Add(
                new RemoteSourceRegistration
                {
                    Name = "Seedbox",
                    SourceClassName = "Ftp",
                    SerializedConfig = unreadableValue,
                    IsActive = true,
                }
            );
            arrangeContext.NfoDatabaseRegistrations.Add(
                new NfoDatabaseRegistration
                {
                    NfoDatabaseClassName = "srrDB",
                    SerializedConfig = unreadableValue,
                    IsActive = true,
                }
            );
            arrangeContext.MediaDatabaseRegistrations.Add(
                new MediaDatabaseRegistration
                {
                    MediaDatabaseClassName = "Tmdb",
                    SerializedConfig = unreadableValue,
                    IsActive = true,
                }
            );
            await arrangeContext.SaveChangesAsync();
        }

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.HosterRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.LinkCrypterRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.ImageHosterRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.DistributionSiteRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.RemoteSourceRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.NfoDatabaseRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
        (await assertContext.MediaDatabaseRegistrations.SingleAsync()).IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task DetectAsync_ActiveRegistrationWithReadableSecrets_KeepsItActive()
    {
        // Arrange
        await AddHosterRegistrationAsync(
            "Hoster",
            currentKeyProtector.Protect("secret"),
            isActive: true
        );

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.HosterRegistrations.SingleAsync()).IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task DetectAsync_DeactivatedRegistrationReadableAgain_ClearsFlagWithoutReactivating()
    {
        // Arrange
        await AddHosterRegistrationAsync(
            "Hoster",
            currentKeyProtector.Protect("secret"),
            hasUnreadableSecrets: true,
            isActive: false
        );

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        var registration = await assertContext.HosterRegistrations.SingleAsync();
        registration.HasUnreadableSecrets.ShouldBeFalse();
        registration.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task DetectAsync_RunTwiceWithUnreadableSecret_CreatesNotificationOnlyOnce()
    {
        // Arrange
        await AddHosterRegistrationAsync("Hoster", otherKeyProtector.Protect("secret"));

        // Act
        await DetectAsync();
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.HosterRegistrations.SingleAsync()).HasUnreadableSecrets.ShouldBeTrue();
        (await assertContext.Notifications.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task DetectAsync_FlaggedRegistrationReadableAgain_ClearsFlagWithoutNotification()
    {
        // Arrange
        await AddHosterRegistrationAsync(
            "Hoster",
            currentKeyProtector.Protect("secret"),
            hasUnreadableSecrets: true
        );

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (
            await assertContext.HosterRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeFalse();
        (await assertContext.Notifications.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task DetectAsync_AllSecretsReadableAgain_ResolvesUnreadableSecretsNotification()
    {
        // Arrange
        await AddHosterRegistrationAsync(
            "Hoster",
            currentKeyProtector.Protect("secret"),
            hasUnreadableSecrets: true
        );
        await AddUnreadableSecretsNotificationAsync();

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.Notifications.SingleAsync()).ResolvedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task DetectAsync_SecretStillUnreadable_KeepsUnreadableSecretsNotificationUnresolved()
    {
        // Arrange
        await AddHosterRegistrationAsync(
            "Hoster",
            otherKeyProtector.Protect("secret"),
            hasUnreadableSecrets: true
        );
        await AddUnreadableSecretsNotificationAsync();

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.Notifications.SingleAsync()).ResolvedAt.ShouldBeNull();
    }

    [Test]
    public async Task DetectAsync_PlaintextLegacyConfig_DoesNotFlagRegistration()
    {
        // Arrange
        await AddHosterRegistrationAsync("Hoster", PlaintextConfig);

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (
            await assertContext.HosterRegistrations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeFalse();
        (await assertContext.Notifications.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task DetectAsync_UnreadableSessionOnly_DiscardsSessionWithoutFlaggingRegistration()
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.DistributionSiteRegistrations.Add(
                CreateDistributionSiteRegistration(
                    "Forum",
                    currentKeyProtector.Protect("config"),
                    session: otherKeyProtector.Protect("session")
                )
            );
            await arrangeContext.SaveChangesAsync();
        }

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        var registration = await assertContext.DistributionSiteRegistrations.SingleAsync();
        registration.EncryptedSession.ShouldBeNull();
        registration.HasUnreadableSecrets.ShouldBeFalse();
        (await assertContext.Notifications.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task DetectAsync_ReadableSession_KeepsSession()
    {
        // Arrange
        var session = currentKeyProtector.Protect("session");
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.DistributionSiteRegistrations.Add(
                CreateDistributionSiteRegistration("Forum", PlaintextConfig, session)
            );
            await arrangeContext.SaveChangesAsync();
        }

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (await assertContext.DistributionSiteRegistrations.SingleAsync()).EncryptedSession.ShouldBe(
            session
        );
    }

    [Test]
    public async Task DetectAsync_UnreadableTelegramBotToken_FlagsConfigurationAndCreatesNotification()
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.TelegramConfigurations.Add(
                CreateTelegramConfiguration(otherKeyProtector.Protect("token"))
            );
            await arrangeContext.SaveChangesAsync();
        }

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (
            await assertContext.TelegramConfigurations.SingleAsync()
        ).HasUnreadableSecrets.ShouldBeTrue();
        var notification = await assertContext.Notifications.SingleAsync();
        notification.Message.ShouldEndWith(": Telegram bot token");
    }

    [Test]
    public async Task DetectAsync_OneRegistrationAlreadyFlagged_NotificationListsOnlyNewlyFlaggedRegistration()
    {
        // Arrange
        var unreadableValue = otherKeyProtector.Protect("secret");
        await AddHosterRegistrationAsync("Known", unreadableValue, hasUnreadableSecrets: true);
        await AddHosterRegistrationAsync("New", unreadableValue);

        // Act
        await DetectAsync();

        // Assert
        var assertContext = CreateDbContext();
        (
            await assertContext.HosterRegistrations.AllAsync(r => r.HasUnreadableSecrets)
        ).ShouldBeTrue();
        var notification = await assertContext.Notifications.SingleAsync();
        notification.Message.ShouldEndWith(": Hoster: New");
    }

    private async Task DetectAsync()
    {
        var dbContext = CreateDbContext();
        var service = new UnreadableSecretsDetectionService(
            repository: new UnreadableSecretsRepository(dbContext),
            secretProtector: currentKeyProtector,
            notificationService: new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            unreadableSecretsNotificationService: UnreadableSecretsNotificationServiceFactory.Create(
                dbContext,
                CreateNotificationConfigurationProvider()
            ),
            logger: NullLogger<UnreadableSecretsDetectionService>.Instance
        );

        await service.DetectAsync(CancellationToken.None);
    }

    private async Task AddHosterRegistrationAsync(
        string name,
        string serializedConfig,
        bool hasUnreadableSecrets = false,
        bool isActive = false
    )
    {
        await using var arrangeContext = Database.CreateDbContext();
        var registration = CreateHosterRegistration(name, serializedConfig);
        registration.HasUnreadableSecrets = hasUnreadableSecrets;
        registration.IsActive = isActive;
        arrangeContext.HosterRegistrations.Add(registration);
        await arrangeContext.SaveChangesAsync();
    }

    private async Task AddUnreadableSecretsNotificationAsync()
    {
        await using var arrangeContext = Database.CreateDbContext();
        arrangeContext.Notifications.Add(
            new Notification
            {
                NotificationKind = NotificationKind.UnreadableSecretsDetected,
                NotificationSeverity = NotificationSeverity.Error,
                Message = "Stored credentials could not be decrypted",
                CreatedAt = DateTime.UtcNow,
            }
        );
        await arrangeContext.SaveChangesAsync();
    }

    private static HosterRegistration CreateHosterRegistration(
        string name,
        string serializedConfig
    ) =>
        new()
        {
            Name = name,
            HosterClassName = "Rapidgator",
            SerializedConfig = serializedConfig,
        };

    private static DistributionSiteRegistration CreateDistributionSiteRegistration(
        string name,
        string serializedConfig,
        string? session
    ) =>
        new()
        {
            Name = name,
            DistributionSiteClassName = "XenForo",
            SerializedConfig = serializedConfig,
            EncryptedSession = session,
        };

    private static TelegramConfiguration CreateTelegramConfiguration(string encryptedBotToken) =>
        new()
        {
            EncryptedBotToken = encryptedBotToken,
            BotUsername = "bearcat_bot",
            NotificationBaseUrl = "http://localhost",
        };

    private static TimeProvider CreateTimeProvider() =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" }
                )
                .Build()
        );
}
