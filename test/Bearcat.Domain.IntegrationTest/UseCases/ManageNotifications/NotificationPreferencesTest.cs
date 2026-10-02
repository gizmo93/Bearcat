using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Configuration;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications;

public class NotificationPreferencesTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private ServiceProvider services = null!;
    private ApplicationConfigurationRegistry registry = null!;
    private ApplicationConfigurationOverrideCache cache = null!;
    private ApplicationConfigurationService settings = null!;
    private NotificationService notifications = null!;
    private TimeProvider timeProvider = null!;

    [SetUp]
    public void Setup()
    {
        var repository = new ApplicationConfigurationOverrideRepository(ReadDbContext, DbContext);
        services = new ServiceCollection()
            .AddSingleton<IApplicationConfigurationOverrideReadRepository>(repository)
            .BuildServiceProvider();
        registry = new ApplicationConfigurationRegistry([
            new ApplicationConfigurationRegistration(typeof(NotificationConfiguration)),
        ]);
        timeProvider = new TimeProvider(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" }
                )
                .Build()
        );
        cache = CreateCache();
        settings = new ApplicationConfigurationService(
            registry: registry,
            readRepository: repository,
            writeRepository: repository,
            overrideCache: cache,
            timeProvider: timeProvider
        );
        notifications = CreateNotificationService(cache);
    }

    [TearDown]
    public async Task DisposeServicesAsync()
    {
        await services.DisposeAsync();
    }

    [Test]
    public async Task Preferences_PersistAcrossCacheReloadAndReset_OnlyAffectFutureNotifications()
    {
        await CreateUploadCompletedAsync("Before disabling");
        var existing = await ReadDbContext.Notifications.SingleAsync();

        await DisableUploadCompletedAsync();
        var savedOverride = await ReadDbContext.ApplicationConfigurationOverrides.SingleAsync();
        savedOverride.SerializedValue.ShouldBe("false");
        await CreateUploadCompletedAsync("Suppressed immediately");

        var reloadedCache = CreateCache();
        await reloadedCache.RefreshAsync(CancellationToken.None);
        var reloadedNotifications = CreateNotificationService(reloadedCache);
        await reloadedNotifications.CreateAsync(
            kind: NotificationKind.UploadCompleted,
            message: "Suppressed after restart",
            cancellationToken: CancellationToken.None
        );

        var remaining = await ReadDbContext.Notifications.SingleAsync();
        remaining.Id.ShouldBe(existing.Id);
        remaining.Message.ShouldBe(existing.Message);
        remaining.ResolvedAt.ShouldBeNull();

        await settings.ResetOverrideAsync(
            configurationKey: "Notifications",
            propertyName: nameof(NotificationConfiguration.UploadCompleted),
            cancellationToken: CancellationToken.None
        );
        (await ReadDbContext.ApplicationConfigurationOverrides.CountAsync()).ShouldBe(0);
        (await ReadDbContext.Notifications.CountAsync()).ShouldBe(1);

        await reloadedCache.RefreshAsync(CancellationToken.None);
        await reloadedNotifications.CreateAsync(
            kind: NotificationKind.UploadCompleted,
            message: "After enabling",
            cancellationToken: CancellationToken.None
        );

        var messages = await ReadDbContext
            .Notifications.OrderBy(n => n.Id)
            .Select(n => n.Message)
            .ToListAsync();
        messages.ShouldBe(["Before disabling", "After enabling"]);
    }

    [Test]
    public async Task Telegram_DisabledKind_SendsExistingQueueWithoutQueuingNewNotifications()
    {
        DbContext.TelegramConfigurations.Add(
            new TelegramConfiguration
            {
                EncryptedBotToken = "test-token",
                BotUsername = "test_bot",
                NotificationBaseUrl = "http://bearcat.test",
                ChatId = 123,
                ForwardInfo = true,
                ForwardWarning = true,
                ForwardError = true,
            }
        );
        await CreateUploadCompletedAsync("Already queued");
        var existing = await DbContext.Notifications.SingleAsync();
        DbContext.TelegramDeliveries.Add(
            new TelegramDelivery { Notification = existing, CreatedAt = timeProvider.GetLocalNow() }
        );
        await DbContext.SaveChangesAsync();
        await DisableUploadCompletedAsync();
        await CreateUploadCompletedAsync("Must not be queued");

        var client = new Mock<ITelegramClient>(MockBehavior.Strict);
        client
            .Setup(c =>
                c.SendMessageAsync(
                    "test-token",
                    123,
                    It.Is<string>(text => text.Contains("Already queued")),
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask);
        var telegram = new TelegramNotificationService(
            configurationRepository: new TelegramConfigurationRepository(DbContext),
            readRepository: new TelegramNotificationReadRepository(ReadDbContext),
            notificationReadRepository: new NotificationReadRepository(ReadDbContext),
            deliveryRepository: new TelegramDeliveryRepository(DbContext),
            secretProtector: NoOpSecretProtector.Instance,
            telegramClient: client.Object,
            timeProvider: timeProvider,
            configurationCache: new TelegramConfigurationCache(),
            unreadableSecretsNotificationService: UnreadableSecretsNotificationServiceFactory.Create(
                DbContext,
                CreateNotificationConfigurationProvider()
            )
        );

        await telegram.ProcessDeliveriesAsync(CancellationToken.None);

        var delivery = await ReadDbContext.TelegramDeliveries.SingleAsync();
        delivery.NotificationId.ShouldBe(existing.Id);
        delivery.DeliveredAt.ShouldNotBeNull();
        (await ReadDbContext.Notifications.CountAsync()).ShouldBe(1);
        client.VerifyAll();
        client.VerifyNoOtherCalls();
    }

    private ApplicationConfigurationOverrideCache CreateCache() =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ApplicationConfigurationOverrideCache>.Instance
        );

    private NotificationService CreateNotificationService(
        IApplicationConfigurationOverrideCache overrideCache
    ) =>
        new(
            repository: new NotificationRepository(DbContext),
            timeProvider: timeProvider,
            configurationProvider: new ApplicationConfigurationProvider(registry, overrideCache)
        );

    private Task CreateUploadCompletedAsync(string message) =>
        notifications.CreateAsync(
            kind: NotificationKind.UploadCompleted,
            message: message,
            cancellationToken: CancellationToken.None
        );

    private Task DisableUploadCompletedAsync() =>
        settings.SaveOverrideAsync(
            configurationKey: "Notifications",
            propertyName: nameof(NotificationConfiguration.UploadCompleted),
            value: false,
            cancellationToken: CancellationToken.None
        );
}
