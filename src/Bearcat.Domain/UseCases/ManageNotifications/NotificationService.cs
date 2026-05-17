using System.Linq.Expressions;
using System.Reflection;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications;

public class NotificationService(INotificationRepository repository, TimeProvider timeProvider)
    : INotificationService
{
    public async Task CreateInfoAsync(string message, CancellationToken cancellationToken)
    {
        await CreateAsync(NotificationType.Info, message, cancellationToken);
    }

    public async Task CreateWarningAsync(string message, CancellationToken cancellationToken)
    {
        await CreateAsync(NotificationType.Warning, message, cancellationToken);
    }

    public async Task CreateErrorAsync(string message, CancellationToken cancellationToken)
    {
        await CreateAsync(NotificationType.Error, message, cancellationToken);
    }

    public async Task ResolveAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        await repository.ResolveAsync(
            notificationId,
            timeProvider.GetLocalNow(),
            cancellationToken
        );
    }

    public async Task ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        await repository.ResolveAllAsync(timeProvider.GetLocalNow(), cancellationToken);
    }

    public void CreateInfo<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        Create(NotificationType.Info, message, entity, selector);
    }

    public void CreateWarning<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        Create(NotificationType.Warning, message, entity, selector);
    }

    public void CreateError<TEntity>(
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        Create(NotificationType.Error, message, entity, selector);
    }

    public void Create<TEntity>(
        NotificationType type,
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        var notification = new Notification
        {
            NotificationType = type,
            Message = message,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        var member = (MemberExpression)selector.Body;
        var property = (PropertyInfo)member.Member;
        property.SetValue(notification, entity, null);

        repository.Add(notification);
    }

    private async Task CreateAsync(
        NotificationType type,
        string message,
        CancellationToken cancellationToken
    )
    {
        var notification = new Notification
        {
            NotificationType = type,
            Message = message,
            CreatedAt = timeProvider.GetLocalNow(),
        };

        repository.Add(notification);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
