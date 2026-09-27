using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class NotificationRepository(IBearcatWriteDbContext dbWrite) : INotificationRepository
{
    public void Add(Notification notification)
    {
        dbWrite.Add(notification);
    }

    public async Task ResolveAsync(
        int notificationId,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    )
    {
        await dbWrite
            .Notifications.Where(n => n.Id == notificationId && n.ResolvedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(n => n.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }

    public async Task ResolveAllAsync(DateTime resolvedAt, CancellationToken cancellationToken)
    {
        await dbWrite
            .Notifications.Where(n => n.ResolvedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(n => n.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }

    public async Task ResolveAllOfKindAsync(
        NotificationKind kind,
        DateTime resolvedAt,
        CancellationToken cancellationToken
    )
    {
        await dbWrite
            .Notifications.Where(n => n.NotificationKind == kind && n.ResolvedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(n => n.ResolvedAt, resolvedAt),
                cancellationToken
            );
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
