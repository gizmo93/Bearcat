using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    
    public NotificationType NotificationType { get; set; }
    
    public string Message { get; set; } = null!;
}
