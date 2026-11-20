using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPackingExcludes;

public class ReleaseFolderPackingExcludeService(
    IFileSystemService fileSystemService,
    IApplicationConfigurationProvider configurationProvider
)
{
    public ExcludedReleaseFolderEntries FindExcludedEntries(string releaseFolderPath)
    {
        var patterns = PackingExcludePattern.ParsePatterns(
            configurationProvider.GetValue<ArchiveCreationConfiguration>(configuration =>
                configuration.PackingExcludePatterns
            )!
        );

        var excludedPaths = FindExcludedPaths(releaseFolderPath, patterns);
        var fileCountsAndSizes = excludedPaths.Select(GetFileCountAndSize).ToList();

        return new ExcludedReleaseFolderEntries(
            RelativePaths: excludedPaths
                .Select(path => Path.GetRelativePath(releaseFolderPath, path))
                .ToList(),
            FileCount: fileCountsAndSizes.Sum(fileCountAndSize => fileCountAndSize.FileCount),
            TotalBytes: fileCountsAndSizes.Sum(fileCountAndSize => fileCountAndSize.TotalBytes)
        );
    }

    private List<string> FindExcludedPaths(string folderPath, List<PackingExcludePattern> patterns)
    {
        var excludedPaths = fileSystemService
            .GetFilesInPath(folderPath, recursive: false)
            .Where(filePath => IsExcluded(patterns, filePath, isFolder: false))
            .ToList();

        foreach (var subfolderPath in fileSystemService.GetFoldersInPath(folderPath))
        {
            if (IsExcluded(patterns, subfolderPath, isFolder: true))
            {
                excludedPaths.Add(subfolderPath);
            }
            else
            {
                excludedPaths.AddRange(FindExcludedPaths(subfolderPath, patterns));
            }
        }

        return excludedPaths;
    }

    private FolderFileCountAndSize GetFileCountAndSize(string excludedPath)
    {
        return fileSystemService.DirectoryExists(excludedPath)
            ? fileSystemService.GetFolderFileCountAndSize(excludedPath)
            : new FolderFileCountAndSize(1, fileSystemService.GetFileSizeBytes(excludedPath));
    }

    private static bool IsExcluded(List<PackingExcludePattern> patterns, string path, bool isFolder)
    {
        var name = Path.GetFileName(path);

        return patterns.Any(pattern =>
            pattern.AppliesToFolders == isFolder && pattern.Matches(name)
        );
    }
}
