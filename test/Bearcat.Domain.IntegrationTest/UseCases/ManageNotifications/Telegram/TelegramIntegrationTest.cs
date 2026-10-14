using System.Net;
using System.Text;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.Infrastructure.Telegram;
using Bearcat.IntegrationTest.Utils;
using Microsoft.Extensions.Configuration;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications.Telegram;

public abstract class TelegramIntegrationTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private TelegramConfigurationCache configurationCache = null!;
    private TelegramConfigurationService configurationService = null!;
    private TelegramClient telegramClient = null!;
    private TimeProvider timeProvider = null!;

    protected TelegramHttpMessageHandler TelegramHttpHandler { get; private set; } = null!;

    [SetUp]
    public void SetUpTelegramHttpHandlerAndConfigurationService()
    {
        TelegramHttpHandler = new TelegramHttpMessageHandler();
        telegramClient = new TelegramClient(new TestHttpClientFactory(TelegramHttpHandler));
        timeProvider = CreateTimeProvider();
        configurationCache = new TelegramConfigurationCache();
        configurationService = new TelegramConfigurationService(
            new TelegramConfigurationRepository(DbContext),
            new TelegramNotificationReadRepository(ReadDbContext),
            configurationCache
        );
    }

    [TearDown]
    public void DisposeTelegramHttpHandler()
    {
        TelegramHttpHandler.Dispose();
    }

    protected TelegramSettingsService CreateSettingsService()
    {
        return new TelegramSettingsService(
            configurationService,
            new TelegramConfigurationRepository(DbContext),
            new TelegramNotificationReadRepository(ReadDbContext),
            new TelegramDeliveryRepository(DbContext),
            NoOpSecretProtector.Instance,
            telegramClient,
            timeProvider,
            UnreadableSecretsNotificationServiceFactory.Create(
                DbContext,
                CreateNotificationConfigurationProvider()
            )
        );
    }

    protected TelegramPairingService CreatePairingService()
    {
        return new TelegramPairingService(
            configurationService,
            new TelegramNotificationReadRepository(ReadDbContext),
            new TelegramDeliveryRepository(DbContext),
            NoOpSecretProtector.Instance,
            telegramClient,
            timeProvider,
            configurationCache
        );
    }

    protected TelegramDeliveryService CreateDeliveryService()
    {
        return new TelegramDeliveryService(
            configurationService,
            new TelegramNotificationReadRepository(ReadDbContext),
            new NotificationReadRepository(ReadDbContext),
            new TelegramDeliveryRepository(DbContext),
            NoOpSecretProtector.Instance,
            telegramClient,
            timeProvider
        );
    }

    protected static TelegramConfiguration CreateConnectedConfiguration(
        bool hasUnreadableSecrets
    ) =>
        new()
        {
            EncryptedBotToken = "encrypted-with-lost-key",
            HasUnreadableSecrets = hasUnreadableSecrets,
            BotUsername = "bearcat_bot",
            NotificationBaseUrl = "http://bearcat.internal",
            ChatId = 987654321,
            ChatName = "Gizmo",
            ForwardNotificationsAfterId = 0,
        };

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();
        return new TimeProvider(configuration);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    protected sealed class TelegramHttpMessageHandler : HttpMessageHandler
    {
        public string? PairingToken { get; set; }

        public string LastSentMessage { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var method = request.RequestUri!.Segments.Last();
            var json = method switch
            {
                "getMe" =>
                    """{"ok":true,"result":{"id":123456,"is_bot":true,"first_name":"Bearcat","username":"bearcat_bot"}}""",
                "getUpdates" => CreateUpdateResponse(),
                "sendMessage" => await CreateSendMessageResponseAsync(request, cancellationToken),
                _ => throw new InvalidOperationException($"Unexpected Telegram method: {method}"),
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }

        private string CreateUpdateResponse()
        {
            return """{"ok":true,"result":[{"update_id":1,"message":{"message_id":1,"date":1700000000,"chat":{"id":987654321,"type":"private","first_name":"Gizmo"},"text":"/start __TOKEN__"}}]}""".Replace(
                "__TOKEN__",
                PairingToken,
                StringComparison.Ordinal
            );
        }

        private async Task<string> CreateSendMessageResponseAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastSentMessage = Uri.UnescapeDataString(
                (await request.Content!.ReadAsStringAsync(cancellationToken)).Replace('+', ' ')
            );
            return """{"ok":true,"result":{"message_id":2,"date":1700000000,"chat":{"id":987654321,"type":"private","first_name":"Gizmo"},"text":"sent"}}""";
        }
    }
}
