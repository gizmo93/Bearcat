using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared;

public class HosterCredentialsRejectionService(INotificationService notificationService)
{
    public async Task DeactivateAndNotifyAsync(
        HosterRegistration registration,
        string message,
        CancellationToken cancellationToken
    )
    {
        if (!TryDeactivate(registration))
        {
            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.HosterCredentialsRejected,
            message: CreateMessage(registration, message),
            cancellationToken: cancellationToken
        );
    }

    public void DeactivateAndNotify(Upload upload, string message)
    {
        var registration = upload.UploadConfig.HosterRegistration;

        if (!TryDeactivate(registration))
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.HosterCredentialsRejected,
            message: CreateMessage(registration, message),
            entity: upload,
            selector: n => n.Upload
        );
    }

    private static bool TryDeactivate(HosterRegistration registration)
    {
        if (!registration.IsActive)
        {
            return false;
        }

        registration.IsActive = false;

        return true;
    }

    private static string CreateMessage(HosterRegistration registration, string message)
    {
        return $"Hoster registration '{registration.Name}' was deactivated because the hoster rejected the credentials: {message}";
    }
}
