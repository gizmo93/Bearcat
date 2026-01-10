using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageNotifications.Repositories;

public interface INotificationRepository
{
    public void Add(Notification notification);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
