namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record FirstStartSetupStatus(
    FirstStartSetupDatabaseStatus DatabaseStatus,
    List<WorkingDirectoryStatus> WorkingDirectoryStatuses
)
{
    public bool HasExistingWritableWorkingDirectory()
    {
        return WorkingDirectoryStatuses.Any(workingDirectoryStatus =>
            workingDirectoryStatus is { Exists: true, IsWritable: true }
        );
    }
}
