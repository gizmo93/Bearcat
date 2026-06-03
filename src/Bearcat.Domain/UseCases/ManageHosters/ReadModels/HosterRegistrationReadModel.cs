namespace Bearcat.Domain.UseCases.ManageHosters.ReadModels;

public record HosterRegistrationReadModel(
    int Id,
    string Name,
    bool IsActive,
    bool RequiresCaptchaVerification,
    bool SupportsCaptchaVerification,
    bool SupportsPremiumOnlyDownloads,
    string HosterName,
    string FullClassName
);
