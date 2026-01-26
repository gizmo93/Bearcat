using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageNotifications.Repositories;
using Bearcat.Domain.ValueObjects;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageNotifications;

public class NotificationService(
    INotificationRepository repository,
    TimeProvider timeProvider) : INotificationService
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
            CreatedAt = timeProvider.GetLocalNow(),
        };

        repository.Add(notification);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
