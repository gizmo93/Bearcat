using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared;

public class HosterCaptchaVerificationService(INotificationService notificationService)
{
    public async Task MarkRequiredAsync(
        HosterRegistration registration,
        string message,
        CancellationToken cancellationToken
    )
    {
        if (!TryMarkRequired(registration))
        {
            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.CaptchaVerificationRequired,
            message: CreateMessage(registration, message),
            cancellationToken: cancellationToken
        );
    }

    public void MarkRequired(Upload upload, string message)
    {
        var registration = upload.UploadConfig.HosterRegistration;

        if (!TryMarkRequired(registration))
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.CaptchaVerificationRequired,
            message: CreateMessage(registration, message),
            entity: upload,
            selector: n => n.Upload
        );
    }

    public static void Clear(HosterRegistration registration, bool activate)
    {
        registration.RequiresCaptchaVerification = false;
        registration.IsActive = activate || registration.IsActive;
    }

    private static bool TryMarkRequired(HosterRegistration registration)
    {
        var alreadyRequired = registration.RequiresCaptchaVerification;

        registration.RequiresCaptchaVerification = true;
        registration.IsActive = false;

        return !alreadyRequired;
    }

    private static string CreateMessage(HosterRegistration registration, string message)
    {
        return $"Hoster registration '{registration.Name}' requires captcha verification: {message}";
    }
}
