using System.Linq.Expressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.PostToForums;

public sealed class FakeNotificationService : INotificationService
{
    public List<CreatedNotification> Created { get; } = [];

    public Task CreateAsync(
        NotificationKind kind,
        string message,
        CancellationToken cancellationToken
    )
    {
        Created.Add(new CreatedNotification(kind, message));

        return Task.CompletedTask;
    }

    public Task ResolveAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Create<TEntity>(
        NotificationKind kind,
        string message,
        TEntity entity,
        Expression<Func<Notification, TEntity?>> selector
    )
    {
        Created.Add(new CreatedNotification(kind, message));
    }
}
