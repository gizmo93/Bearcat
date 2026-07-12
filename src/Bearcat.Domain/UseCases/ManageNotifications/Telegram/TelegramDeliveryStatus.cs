namespace Bearcat.Domain.UseCases.ManageNotifications.Telegram;

public sealed record TelegramDeliveryStatus(
    int PendingCount,
    int FailedCount,
    DateTime? LastDeliveredAt,
    string? LastError
);
