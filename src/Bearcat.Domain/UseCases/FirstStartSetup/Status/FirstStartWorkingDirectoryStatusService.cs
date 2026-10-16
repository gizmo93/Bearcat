using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Microsoft.Extensions.Options;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public class FirstStartWorkingDirectoryStatusService(
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    public List<WorkingDirectoryStatus> GetWorkingDirectoryStatuses()
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
