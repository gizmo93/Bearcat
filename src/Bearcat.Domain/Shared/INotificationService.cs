using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared;

public interface INotificationService
{
    Task CreateAsync(NotificationKind kind, string message, CancellationToken cancellationToken);
    Task ResolveAsync(int notificationId, CancellationToken cancellationToken = default);
    Task ResolveAllAsync(CancellationToken cancellationToken = default);

    void Create<TEntity>(
        NotificationKind kind,
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    );
}
