namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed record TelegramSettings(
    bool IsConfigured,
    bool HasUnreadableSecrets,
    string? BotUsername,
    string NotificationBaseUrl,
    bool IsConnected,
    string? ChatName,
    bool ForwardInfo,
    bool ForwardWarning,
    bool ForwardError,
    bool IsPairing
);
