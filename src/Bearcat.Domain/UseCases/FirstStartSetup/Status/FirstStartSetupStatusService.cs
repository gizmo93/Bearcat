using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Microsoft.Extensions.Options;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public class FirstStartSetupStatusService(
    IFirstStartSetupStatusRepository statusRepository,
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    public async Task<FirstStartSetupStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var databaseStatus = await statusRepository.GetDatabaseStatusAsync(cancellationToken);

        return new FirstStartSetupStatus(
            databaseStatus.HasHosterRegistration,
            databaseStatus.HasReleaseTemplate,
            databaseStatus.HasReleaseFolderAutomationOrRemoteSourceAutomation,
            databaseStatus.HasRelease,
            GetWorkingDirectoryStatuses()
        );
    }

    private List<WorkingDirectoryStatus> GetWorkingDirectoryStatuses()
    {
        return workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Where(workingDirectory => !string.IsNullOrWhiteSpace(workingDirectory))
            .Select(GetWorkingDirectoryStatus)
            .ToList();
    }

    private WorkingDirectoryStatus GetWorkingDirectoryStatus(string workingDirectory)
    {
        if (!fileSystemService.DirectoryExists(workingDirectory))
        {
            return new WorkingDirectoryStatus(workingDirectory, Exists: false, IsWritable: false);
        }

        return new WorkingDirectoryStatus(
            workingDirectory,
            Exists: true,
            IsWritable: fileSystemService.IsDirectoryWritable(workingDirectory)
        );
    }
}
