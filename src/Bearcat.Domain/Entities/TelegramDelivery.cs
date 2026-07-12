namespace Bearcat.Domain.Entities;

public class TelegramDelivery
{
    public int Id { get; set; }

    public int NotificationId { get; set; }

    public Notification Notification { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? NextAttemptAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
