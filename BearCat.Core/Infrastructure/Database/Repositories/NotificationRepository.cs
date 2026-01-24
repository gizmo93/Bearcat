using BearCat.Core.Domain.Entities;
using BearCat.Core.Domain.UseCases.ManageNotifications.Repositories;

namespace BearCat.Core.Infrastructure.Database.Repositories;

public class NotificationRepository(IBearcatWriteDbContext dbWrite)
    : INotificationRepository
{
    public void Add(Notification notification)
    {
        dbWrite.Add(notification);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
