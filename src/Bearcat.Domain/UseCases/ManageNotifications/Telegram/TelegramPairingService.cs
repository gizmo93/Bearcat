using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed class TelegramPairingService(
    TelegramConfigurationService configurationService,
    ITelegramNotificationReadRepository readRepository,
    ITelegramDeliveryRepository deliveryRepository,
    ISecretProtector secretProtector,
    ITelegramClient telegramClient,
    TimeProvider timeProvider,
    TelegramConfigurationCache configurationCache
)
{
    private static readonly TimeSpan PairingDuration = TimeSpan.FromMinutes(10);
    private const int PairingTokenByteCount = 32;

    public bool HasPendingPairing => configurationCache.Current?.PairingTokenHash is not null;

    public async Task<string> CreatePairingLinkAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);
        var token = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(PairingTokenByteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        configuration.PairingTokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))
        );

        configuration.PairingExpiresAt = timeProvider.GetLocalNow().Add(PairingDuration);
        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);

        return $"https://t.me/{configuration.BotUsername}?start={token}";
    }

    public async Task DisconnectChatAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);

        configuration.ChatId = null;
        configuration.ChatName = null;
        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;
        configuration.ForwardNotificationsAfterId =
            await readRepository.GetLatestNotificationIdAsync(cancellationToken);

        await deliveryRepository.DeletePendingAsync(cancellationToken);
        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
    }

    public async Task ConnectChatFromReceivedStartMessagesAsync(CancellationToken cancellationToken)
    {
        var pairing = await configurationService.GetCachedStateWithReadableBotTokenAsync(
            cancellationToken
        );
        if (pairing?.PairingTokenHash is null || pairing.PairingExpiresAt is null)
        {
            return;
        }

        if (pairing.PairingExpiresAt <= timeProvider.GetLocalNow())
        {
            await ClearExpiredPairingAsync(cancellationToken);
            return;
        }

        var updates = await ReceivePairingUpdatesAsync(pairing, cancellationToken);
        if (updates.Count > 0)
        {
            await ApplyPairingUpdatesAsync(updates, cancellationToken);
        }
    }

    private async Task ClearExpiredPairingAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);

        configuration.PairingTokenHash = null;
        configuration.PairingExpiresAt = null;

        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TelegramUpdate>> ReceivePairingUpdatesAsync(
        TelegramConfigurationState pairing,
        CancellationToken cancellationToken
    )
    {
        return await telegramClient.GetUpdatesAsync(
            secretProtector.Unprotect(pairing.EncryptedBotToken),
            (int)pairing.UpdateOffset,
            cancellationToken
        );
    }

    private async Task ApplyPairingUpdatesAsync(
        IReadOnlyList<TelegramUpdate> updates,
        CancellationToken cancellationToken
    )
    {
        var configuration = await configurationService.GetConfigurationAsync(cancellationToken);
        configuration.UpdateOffset = updates.Last().UpdateId + 1;

        foreach (var update in updates)
        {
            if (
                update.Chat is null
                || !TryReadPairingToken(update.Text, out var token)
                || !IsValidPairingToken(configuration, token)
            )
            {
                continue;
            }

            configuration.ChatId = update.Chat.Id;
            configuration.ChatName = GetChatName(update.Chat);
            configuration.PairingTokenHash = null;
            configuration.PairingExpiresAt = null;
            configuration.ForwardNotificationsAfterId =
                await readRepository.GetLatestNotificationIdAsync(cancellationToken);

            break;
        }

        await configurationService.SaveChangesAndInvalidateCacheAsync(cancellationToken);
    }

    private static bool TryReadPairingToken(string? text, out string token)
    {
        const string command = "/start ";

        if (text?.StartsWith(command, StringComparison.Ordinal) == true)
        {
            token = text[command.Length..].Trim();
            return token.Length > 0;
        }

        token = string.Empty;
        return false;
    }

    private bool IsValidPairingToken(TelegramConfiguration configuration, string token)
    {
        return configuration.PairingTokenHash is not null
            && configuration.PairingExpiresAt > timeProvider.GetLocalNow()
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(configuration.PairingTokenHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(token))
            );
    }

    private static string GetChatName(TelegramChat chat)
    {
        var name = string.Join(
            ' ',
            new[] { chat.FirstName, chat.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))
        );
        return string.IsNullOrWhiteSpace(name)
            ? chat.Username ?? chat.Id.ToString(CultureInfo.InvariantCulture)
            : name;
    }
}
