using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public NotificationType NotificationType { get; set; }

    public string Message { get; set; } = null!;
    
    public UploadNotification? UploadNotification { get; set; }
    
    public ArchiveNotification? ArchiveNotification { get; set; }
    
    public LinkCrypterContainerNotification? LinkCrypterContainerNotification { get; set; }
}
