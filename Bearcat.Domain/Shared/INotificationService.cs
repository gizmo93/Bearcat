using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared;

public interface INotificationService
{
    Task CreateInfoAsync(string message, CancellationToken cancellationToken);
    Task CreateWarningAsync(string message, CancellationToken cancellationToken);
    Task CreateErrorAsync(string message, CancellationToken cancellationToken);
    Task ResolveAsync(int notificationId, CancellationToken cancellationToken = default);
    Task ResolveAllAsync(CancellationToken cancellationToken = default);

    void CreateInfo<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    );

    void CreateWarning<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    );

    void CreateError<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    );

    void Create<TEntity>(
        NotificationType type,
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    );
}
