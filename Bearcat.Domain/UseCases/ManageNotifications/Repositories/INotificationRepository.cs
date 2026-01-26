using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageNotifications.Repositories;

public interface INotificationRepository
{
    public void Add(Notification notification);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
