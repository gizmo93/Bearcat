namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartSetupDatabaseStatus(
    bool HasHosterRegistration,
    bool HasReleaseTemplate,
    bool HasReleaseFolderAutomationOrRemoteSourceAutomation,
    bool HasRelease,
    FirstStartOptionalSetupStatus OptionalSetupStatus
)
{
    public bool IsFreshInstallation => !HasHosterRegistration && !HasRelease;

    public bool IsRequiredSetupComplete =>
        HasHosterRegistration
        && HasReleaseTemplate
        && HasReleaseFolderAutomationOrRemoteSourceAutomation;
}
