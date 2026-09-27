using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.DetectUnreadableSecrets;

public class UnreadableSecretsRepositoryTest : BearcatIntegrationTest
{
    private const string Config = "config";

    private static readonly Action<BearcatDbContext>[] AddEntityWithUnreadableSecrets =
    [
        dbContext =>
            dbContext.HosterRegistrations.Add(
                new HosterRegistration
                {
                    Name = "Hoster",
                    HosterClassName = "Rapidgator",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.LinkCrypterRegistrations.Add(
                new LinkCrypterRegistration
                {
                    Name = "Crypter",
                    LinkCrypterClassName = "FileCrypt",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.ImageHosterRegistrations.Add(
                new ImageHosterRegistration
                {
                    Name = "Images",
                    ImageHosterClassName = "ImgBb",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.DistributionSiteRegistrations.Add(
                new DistributionSiteRegistration
                {
                    Name = "Forum",
                    DistributionSiteClassName = "XenForo",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.RemoteSourceRegistrations.Add(
                new RemoteSourceRegistration
                {
                    Name = "Seedbox",
                    SourceClassName = "Ftp",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.NfoDatabaseRegistrations.Add(
                new NfoDatabaseRegistration
                {
                    NfoDatabaseClassName = "srrDB",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.MediaDatabaseRegistrations.Add(
                new MediaDatabaseRegistration
                {
                    MediaDatabaseClassName = "Tmdb",
                    SerializedConfig = Config,
                    HasUnreadableSecrets = true,
                }
            ),
        dbContext =>
            dbContext.TelegramConfigurations.Add(
                new TelegramConfiguration
                {
                    EncryptedBotToken = Config,
                    BotUsername = "bearcat_bot",
                    NotificationBaseUrl = "http://localhost",
                    HasUnreadableSecrets = true,
                }
            ),
    ];

    [Test]
    public async Task AnyUnreadableSecretsAsync_NoEntityFlagged_ReturnsFalse()
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.HosterRegistrations.Add(
                new HosterRegistration
                {
                    Name = "Hoster",
                    HosterClassName = "Rapidgator",
                    SerializedConfig = Config,
                }
            );
            await arrangeContext.SaveChangesAsync();
        }

        var repository = new UnreadableSecretsRepository(CreateDbContext());

        // Act
        var result = await repository.AnyUnreadableSecretsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public async Task AnyUnreadableSecretsAsync_OneEntityFlagged_ReturnsTrue(int entityIndex)
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            AddEntityWithUnreadableSecrets[entityIndex](arrangeContext);
            await arrangeContext.SaveChangesAsync();
        }

        var repository = new UnreadableSecretsRepository(CreateDbContext());

        // Act
        var result = await repository.AnyUnreadableSecretsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task AnyUnresolvedUnreadableSecretsNotificationAsync_OnlyResolvedOrOtherKinds_ReturnsFalse()
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.Notifications.AddRange(
                CreateNotification(NotificationKind.UnreadableSecretsDetected, resolved: true),
                CreateNotification(NotificationKind.UploadFailed, resolved: false)
            );
            await arrangeContext.SaveChangesAsync();
        }

        var repository = new UnreadableSecretsRepository(CreateDbContext());

        // Act
        var result = await repository.AnyUnresolvedUnreadableSecretsNotificationAsync(
            CancellationToken.None
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task AnyUnresolvedUnreadableSecretsNotificationAsync_UnresolvedNotificationExists_ReturnsTrue()
    {
        // Arrange
        await using (var arrangeContext = Database.CreateDbContext())
        {
            arrangeContext.Notifications.Add(
                CreateNotification(NotificationKind.UnreadableSecretsDetected, resolved: false)
            );
            await arrangeContext.SaveChangesAsync();
        }

        var repository = new UnreadableSecretsRepository(CreateDbContext());

        // Act
        var result = await repository.AnyUnresolvedUnreadableSecretsNotificationAsync(
            CancellationToken.None
        );

        // Assert
        result.ShouldBeTrue();
    }

    private static Notification CreateNotification(NotificationKind kind, bool resolved) =>
        new()
        {
            NotificationKind = kind,
            NotificationSeverity = NotificationSeverity.Error,
            Message = "Message",
            CreatedAt = DateTime.UtcNow,
            ResolvedAt = resolved ? DateTime.UtcNow : null,
        };
}
