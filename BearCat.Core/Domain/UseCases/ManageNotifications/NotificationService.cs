using BearCat.Core.Domain.Shared;
using BearCat.Core.Domain.UseCases.ManageNotifications.Repositories;
using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.UseCases.ManageNotifications;

public class NotificationService(INotificationRepository repository) : INotificationService
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

    private async Task CreateAsync(NotificationType type, string message, CancellationToken cancellationToken)
    {
        var notification = new Entities.Notification
        {
            NotificationType = type,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };

        repository.Add(notification);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
