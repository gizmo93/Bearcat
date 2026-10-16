namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartSetupStatus(
    bool HasHosterRegistration,
    bool HasReleaseTemplate,
    bool HasReleaseFolderAutomationOrRemoteSourceAutomation,
    bool HasRelease,
    List<WorkingDirectoryStatus> WorkingDirectoryStatuses
)
{
    public bool IsFreshInstallation => !HasHosterRegistration && !HasRelease;
}
