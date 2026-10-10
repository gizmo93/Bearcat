using Microsoft.Extensions.Options;

namespace Bearcat.Domain.Shared.FolderConfirmation;

public class ConfirmableFolderRootProvider(
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
)
{
    public IReadOnlyList<string> GetRootPaths()
    {
        return workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Where(workingDirectory => !string.IsNullOrWhiteSpace(workingDirectory))
            .Select(workingDirectory => Path.TrimEndingDirectorySeparator(workingDirectory.Trim()))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
