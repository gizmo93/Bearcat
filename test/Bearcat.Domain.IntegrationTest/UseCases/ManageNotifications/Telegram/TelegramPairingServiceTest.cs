using Bearcat.Domain.UseCases.ManageNotifications.Telegram;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNotifications.Telegram;

public class TelegramPairingServiceTest(DatabaseProvider databaseProvider)
    : TelegramIntegrationTest(databaseProvider)
{
    private TelegramSettingsService settingsService = null!;
    private TelegramPairingService service = null!;

    [SetUp]
    public void Setup()
    {
        settingsService = CreateSettingsService();
        service = CreatePairingService();
    }

    [Test]
    public async Task ConnectChatFromReceivedStartMessagesAsync_StartMessageReceived_ConnectsChat()
    {
        await settingsService.SaveConfigurationAsync(
            "1234567:4TT8bAc8GHUspu3ERYn-KGcvsvGB9u_n4ddy",
            "http://bearcat.internal",
            CancellationToken.None
        );
        var pairingUrl = await service.CreatePairingLinkAsync(CancellationToken.None);
        TelegramHttpHandler.PairingToken = new Uri(pairingUrl).Query[7..];

        await service.ConnectChatFromReceivedStartMessagesAsync(CancellationToken.None);

        DbContext.ChangeTracker.Clear();
        var configuration = await DbContext.TelegramConfigurations.SingleAsync();
        configuration.ChatId.ShouldBe(987654321);
        configuration.ChatName.ShouldBe("Gizmo");
        configuration.PairingTokenHash.ShouldBeNull();
    }
}
