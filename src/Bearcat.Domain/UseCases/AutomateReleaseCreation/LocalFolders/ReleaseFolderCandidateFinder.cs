using System.IO.Enumeration;
using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.ManageReleases;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders;

public class ReleaseFolderCandidateFinder(IFileSystemService fileSystemService)
{
    private const string SynologyMetadataFolderName = "@eaDir";

    public List<string> FindMatchingFolderPaths(string basePath, string? folderNamePattern)
    {
        return fileSystemService
            .GetFoldersInPath(basePath)
            .Where(folderPath => MatchesPattern(folderPath, folderNamePattern))
            .ToList();
    }

    private static bool MatchesPattern(string folderPath, string? folderNamePattern)
    {
        if (folderPath.Contains(SynologyMetadataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(folderNamePattern))
        {
            return true;
        }

        return FileSystemName.MatchesSimpleExpression(
            folderNamePattern,
            FolderPathHelper.GetFolderName(folderPath),
            ignoreCase: true
        );
    }
}
