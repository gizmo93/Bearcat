using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface INotificationRepository
{
    public void Add(Notification notification);
    Task ResolveAsync(int notificationId, DateTime resolvedAt, CancellationToken cancellationToken);
    Task ResolveAllAsync(DateTime resolvedAt, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
