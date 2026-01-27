namespace Bearcat.Domain.Entities;

public abstract class EntityNotification<TEntity>
    where TEntity : class
{
    public int NotificationId { get; set; }
    
    public Notification Notification { get; set; } = null!;

    public TEntity Entity { get; set; } = null!;

    public int EntityId { get; set; }
}
