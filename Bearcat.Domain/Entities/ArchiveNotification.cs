namespace Bearcat.Domain.Entities;

public class ArchiveNotification
{
    public int NotificationId { get; set; }

    public int ArchiveId { get; set; }
    
    public Archive Archive { get; set; } = null!;
    
    public Notification Notification { get; set; } = null!;
}
