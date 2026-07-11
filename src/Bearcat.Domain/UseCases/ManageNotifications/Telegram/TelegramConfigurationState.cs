using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed record TelegramConfigurationState(
    string EncryptedBotToken,
    string BotUsername,
    string NotificationBaseUrl,
    long? ChatId,
    string? ChatName,
    bool ForwardInfo,
    bool ForwardWarning,
    bool ForwardError,
    string? PairingTokenHash,
    DateTime? PairingExpiresAt,
    long UpdateOffset,
    int ForwardNotificationsAfterId
)
{
    public static TelegramConfigurationState From(TelegramConfiguration configuration) =>
        new(
            configuration.EncryptedBotToken,
            configuration.BotUsername,
            configuration.NotificationBaseUrl,
            configuration.ChatId,
            configuration.ChatName,
            configuration.ForwardInfo,
            configuration.ForwardWarning,
            configuration.ForwardError,
            configuration.PairingTokenHash,
            configuration.PairingExpiresAt,
            configuration.UpdateOffset,
            configuration.ForwardNotificationsAfterId
        );
}
