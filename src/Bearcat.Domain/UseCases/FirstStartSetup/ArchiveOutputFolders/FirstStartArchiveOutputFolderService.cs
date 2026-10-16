using Bearcat.Abstractions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.FirstStartSetup.Exceptions;
using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.Options;

namespace Bearcat.Domain.UseCases.FirstStartSetup.ArchiveOutputFolders;

public class FirstStartArchiveOutputFolderService(
    IFileSystemService fileSystemService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    public void CreateArchiveOutputFolder(string folderPath)
    {
        var fullFolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));

        var isInsideExistingWorkingDirectory = workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Any(workingDirectory =>
                fileSystemService.DirectoryExists(workingDirectory)
                && IsStrictlyInsideWorkingDirectory(fullFolderPath, workingDirectory)
            );

        if (!isInsideExistingWorkingDirectory)
        {
            throw new ArchiveOutputFolderNotInsideExistingWorkingDirectoryException(folderPath);
        }

        fileSystemService.CreateDirectory(fullFolderPath);
    }

    private static bool IsStrictlyInsideWorkingDirectory(
        string fullFolderPath,
        string workingDirectory
    )
    {
        var fullWorkingDirectoryPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(workingDirectory)
        );

        return FolderPathHelper.IsSameOrSubPath(fullFolderPath, fullWorkingDirectoryPath)
            && !string.Equals(fullFolderPath, fullWorkingDirectoryPath, StringComparison.Ordinal);
    }
}
