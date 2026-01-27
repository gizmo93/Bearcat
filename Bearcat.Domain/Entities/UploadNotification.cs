namespace Bearcat.Domain.Entities;

public class UploadNotification
{
    public int NotificationId { get; set; }
    
    public int UploadId { get; set; }
    
    public Upload Upload { get; set; } = null!;

    
    public Notification Notification { get; set; } = null!;
}
