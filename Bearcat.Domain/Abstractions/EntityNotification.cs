using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Abstractions;

public abstract class EntityNotification<TEntity>
    where TEntity : class, IEntityWithNotifications
{
    public int NotificationId { get; set; }

    public Notification Notification { get; set; } = null!;

    public TEntity Entity { get; set; } = null!;

    public int EntityId { get; set; }
}
