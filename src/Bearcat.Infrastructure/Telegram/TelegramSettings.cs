namespace Bearcat.Infrastructure.Telegram;

public sealed record TelegramSettings(
    bool IsConfigured,
    string? BotUsername,
    string NotificationBaseUrl,
    bool IsConnected,
    string? ChatName,
    bool ForwardInfo,
    bool ForwardWarning,
    bool ForwardError,
    bool IsPairing
);
