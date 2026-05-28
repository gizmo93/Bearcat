using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared;

public class HosterCaptchaVerificationService(INotificationService notificationService)
{
    public async Task MarkRequiredAsync(
        HosterRegistration registration,
        string message,
        CancellationToken cancellationToken
    )
    {
        MarkRequired(registration);

        await notificationService.CreateWarningAsync(
            CreateMessage(registration, message),
            cancellationToken
        );
    }

    public void MarkRequired(Upload upload, string message)
    {
        var registration = upload.UploadConfig.HosterRegistration;
        MarkRequired(registration);

        notificationService.CreateWarning(
            message: CreateMessage(registration, message),
            entity: upload,
            selector: n => n.Upload
        );
    }

    public void Clear(HosterRegistration registration, bool activate)
    {
        registration.RequiresCaptchaVerification = false;
        registration.IsActive = activate || registration.IsActive;
    }

    private static void MarkRequired(HosterRegistration registration)
    {
        registration.RequiresCaptchaVerification = true;
        registration.IsActive = false;
    }

    private static string CreateMessage(HosterRegistration registration, string message)
    {
        return $"Hoster registration '{registration.Name}' requires captcha verification: {message}";
    }
}
