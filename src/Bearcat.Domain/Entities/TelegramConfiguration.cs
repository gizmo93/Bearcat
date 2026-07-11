namespace Bearcat.Domain.Entities;

public class TelegramConfiguration
{
    public int Id { get; set; }

    public string EncryptedBotToken { get; set; } = null!;

    public string BotUsername { get; set; } = null!;

    public string NotificationBaseUrl { get; set; } = null!;

    public long? ChatId { get; set; }

    public string? ChatName { get; set; }

    public bool ForwardInfo { get; set; } = true;

    public bool ForwardWarning { get; set; } = true;

    public bool ForwardError { get; set; } = true;

    public string? PairingTokenHash { get; set; }

    public DateTime? PairingExpiresAt { get; set; }

    public long UpdateOffset { get; set; }

    public int ForwardNotificationsAfterId { get; set; }
}
