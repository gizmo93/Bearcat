namespace Bearcat.Domain.Entities;

public class LinkCrypterContainerNotification
{
    public int NotificationId { get; set; }

    public int LinkCrypterContainerId { get; set; }
    
    public LinkCrypterContainer LinkCrypterContainer { get; set; } = null!;
    
    public Notification Notification { get; set; } = null!;
}
