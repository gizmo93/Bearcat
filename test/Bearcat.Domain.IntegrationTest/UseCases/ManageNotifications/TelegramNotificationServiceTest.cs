using System.Net;
using System.Text;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.Infrastructure.Telegram;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications;

public class TelegramNotificationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext readDbContext = null!;
    private BearcatDbContext writeDbContext = null!;
    private TelegramHttpMessageHandler telegram = null!;
    private TelegramNotificationService service = null!;

    [SetUp]
    public void Setup()
    {
        readDbContext = Database.CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        writeDbContext = Database.CreateDbContext();
        telegram = new TelegramHttpMessageHandler();
        service = new TelegramNotificationService(
            new TelegramConfigurationRepository(writeDbContext),
            new TelegramNotificationReadRepository(readDbContext),
            new NotificationReadRepository(readDbContext),
            new TelegramDeliveryRepository(writeDbContext),
            NoOpSecretProtector.Instance,
            new TelegramClient(new TestHttpClientFactory(telegram)),
            CreateTimeProvider(),
            new TelegramConfigurationCache()
        );
    }

    [TearDown]
    public async Task TearDownAsync()
    {
        await readDbContext.DisposeAsync();
        await writeDbContext.DisposeAsync();
        telegram.Dispose();
    }

    [Test]
    public async Task Pairing_StartMessageReceived_ConnectsChat()
    {
        await service.SaveConfigurationAsync(
            "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy",
            "http://bearcat.internal",
            CancellationToken.None
        );
        var pairingUrl = await service.BeginPairingAsync(CancellationToken.None);
        telegram.PairingToken = new Uri(pairingUrl).Query[7..];

        await service.PollPairingAsync(CancellationToken.None);

        writeDbContext.ChangeTracker.Clear();
        var configuration = await writeDbContext.TelegramConfigurations.SingleAsync();
        configuration.ChatId.ShouldBe(987654321);
        configuration.ChatName.ShouldBe("Gizmo");
        configuration.PairingTokenHash.ShouldBeNull();
    }

    [Test]
    public async Task ProcessDeliveries_ErrorEnabledAndInfoDisabled_SendsOnlyError()
    {
        writeDbContext.TelegramConfigurations.Add(
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
        writeDbContext.Notifications.AddRange(
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationType = NotificationType.Info,
                Message = "Upload completed",
            },
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationType = NotificationType.Error,
                Message = "Upload failed",
            },
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                ResolvedAt = DateTime.UtcNow,
                NotificationType = NotificationType.Error,
                Message = "Already resolved",
            }
        );
        await writeDbContext.SaveChangesAsync();

        await service.ProcessDeliveriesAsync(CancellationToken.None);

        writeDbContext.ChangeTracker.Clear();
        var delivery = await writeDbContext
            .TelegramDeliveries.Include(item => item.Notification)
            .SingleAsync();
        delivery.Notification.NotificationType.ShouldBe(NotificationType.Error);
        delivery.DeliveredAt.ShouldNotBeNull();
        telegram.LastSentMessage.ShouldContain("Upload failed");
        telegram.LastSentMessage.ShouldContain(
            $"http://bearcat.internal/notifications/{delivery.NotificationId}"
        );
    }

    [Test]
    public async Task ProcessDeliveries_NotificationLinkedToRelease_IncludesEntityNameInMessage()
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
        writeDbContext.AddRange(releaseGroup, release);
        writeDbContext.TelegramConfigurations.Add(
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
        writeDbContext.Notifications.Add(
            new Notification
            {
                CreatedAt = DateTime.UtcNow,
                NotificationType = NotificationType.Warning,
                Message = "All files are offline on the hoster",
                Release = release,
            }
        );
        await writeDbContext.SaveChangesAsync();

        await service.ProcessDeliveriesAsync(CancellationToken.None);

        telegram.LastSentMessage.ShouldContain("All files are offline on the hoster");
        telegram.LastSentMessage.ShouldContain("Release: Awesome.Movie.2026.WEB.H265-ZeroTwo");
    }

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

    private sealed class TelegramHttpMessageHandler : HttpMessageHandler
    {
        public string? PairingToken { get; set; }

        public string LastSentMessage { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var method = request.RequestUri!.Segments[^1];
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
