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

    public bool HasExistingWritableWorkingDirectory()
    {
        return WorkingDirectoryStatuses.Any(workingDirectoryStatus =>
            workingDirectoryStatus is { Exists: true, IsWritable: true }
        );
    }
}
