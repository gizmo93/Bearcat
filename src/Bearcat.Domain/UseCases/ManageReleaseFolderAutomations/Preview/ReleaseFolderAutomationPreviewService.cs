using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage.Repositories;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.LocalFolders;
using Bearcat.Domain.UseCases.ManageReleases;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;

public class ReleaseFolderAutomationPreviewService(
    ReleaseFolderCandidateFinder candidateFinder,
    IReleaseFolderUsageRepository releaseFolderUsageRepository,
    IFileSystemService fileSystemService
)
{
    public async Task<ReleaseFolderAutomationPreview> PreviewMatchingFoldersAsync(
        string basePath,
        string? folderNamePattern,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedBasePath = basePath.Trim();

        if (!fileSystemService.DirectoryExists(normalizedBasePath))
        {
            return new ReleaseFolderAutomationPreview(BasePathExists: false, MatchingFolders: []);
        }

        var matchingFolderPaths = await Task.Run(
            () =>
                candidateFinder.FindMatchingFolderPaths(
                    normalizedBasePath,
                    folderNamePattern?.Trim()
                ),
            cancellationToken
        );

        var usedFolderPaths =
            await releaseFolderUsageRepository.GetUsedReleaseArchiveOrRemoteDownloadFolderPathsAsync(
                matchingFolderPaths,
                cancellationToken
            );

        var matchingFolders = matchingFolderPaths
            .Select(folderPath => new ReleaseFolderAutomationPreviewFolder(
                FolderName: FolderPathHelper.GetFolderName(folderPath),
                FolderPath: folderPath,
                IsUsedByReleaseArchiveOrRemoteDownload: usedFolderPaths.Contains(folderPath)
            ))
            .OrderBy(folder => folder.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReleaseFolderAutomationPreview(BasePathExists: true, matchingFolders);
    }
}
