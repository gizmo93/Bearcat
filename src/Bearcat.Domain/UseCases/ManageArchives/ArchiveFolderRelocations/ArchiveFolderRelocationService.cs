using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;

public class ArchiveFolderRelocationService(
    IArchiveFolderRelocationRepository repository,
    IFileSystemService fileSystemService,
    ILogger<ArchiveFolderRelocationService> logger
)
{
    public async Task<IReadOnlyList<ArchiveFolderRelocationPreviewItem>> GetRelocationPreviewAsync(
        IReadOnlyList<int> archiveIds,
        string basePath,
        CancellationToken cancellationToken
    )
    {
        var archives = await repository.GetArchivesWithFilesUploadsAndReleaseAsync(
            archiveIds,
            cancellationToken
        );

        return await CreatePreviewItemsAsync(
            archives: archives,
            archivePartFileNamesByArchiveBaseNameByFolderPath: GetArchivePartFileNamesByArchiveBaseNameByFolderPath(
                basePath
            ),
            cancellationToken: cancellationToken
        );
    }

    public async Task<ArchiveFolderRelocationResult> RelocateAsync(
        IReadOnlyList<int> archiveIds,
        string basePath,
        CancellationToken cancellationToken
    )
    {
        var archives = await repository.GetArchivesWithFilesUploadsAndReleaseAsync(
            archiveIds,
            cancellationToken
        );

        var archivePartFileNamesByArchiveBaseNameByFolderPath =
            GetArchivePartFileNamesByArchiveBaseNameByFolderPath(basePath);

        var previewItems = await CreatePreviewItemsAsync(
            archives: archives,
            archivePartFileNamesByArchiveBaseNameByFolderPath: archivePartFileNamesByArchiveBaseNameByFolderPath,
            cancellationToken: cancellationToken
        );

        var archiveStorageFolders = await repository.GetArchiveStorageFoldersAsync(
            cancellationToken
        );

        var archivesById = archives.ToDictionary(archive => archive.Id);
        var relocatablePreviewItems = previewItems.Where(item => item.IsRelocatable).ToList();

        foreach (var previewItem in relocatablePreviewItems)
        {
            var archive = archivesById[previewItem.ArchiveId];
            var archiveFolderPath = previewItem.FoundArchiveFolderPath!;

            RelocateArchive(
                archive: archive,
                archiveFolderPath: archiveFolderPath,
                archivePartFileNamesInArchiveFolder: archivePartFileNamesByArchiveBaseNameByFolderPath[
                    archiveFolderPath
                ][GetArchiveBaseName(archive)],
                archiveStorageFolders: archiveStorageFolders
            );
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Relocated {RelocatedArchiveCount} of {SelectedArchiveCount} archives to folders below {BasePath}",
            relocatablePreviewItems.Count,
            previewItems.Count,
            basePath
        );

        return new ArchiveFolderRelocationResult(
            RelocatedArchiveCount: relocatablePreviewItems.Count,
            NotRelocatedArchives: previewItems.Where(item => !item.IsRelocatable).ToList()
        );
    }

    private static void RelocateArchive(
        Archive archive,
        string archiveFolderPath,
        HashSet<string> archivePartFileNamesInArchiveFolder,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders
    )
    {
        UnmanagedReleaseArchiveInitializationService.RepointArchive(archive, archiveFolderPath);

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archivePartFileNamesInArchiveFolder.TryGetValue(
                Path.GetFileName(archiveFile.FullFileName),
                out var fileNameOnDisk
            );

            archiveFile.FullFileName = Path.Combine(archiveFolderPath, fileNameOnDisk!);
        }

        UnmanagedReleaseArchiveInitializationService.AssignArchiveStorageFolderContainingArchiveFolder(
            archive,
            archiveStorageFolders
        );

        if (archive.ArchiveConfig.Release.ReleaseType is ReleaseType.Unmanaged)
        {
            archive.ArchiveConfig.ArchiveFilesBasePath = archiveFolderPath;
        }

        archive.MarkAsCreatedAndSetWaitingUploadsPending();
    }

    private async Task<List<ArchiveFolderRelocationPreviewItem>> CreatePreviewItemsAsync(
        IReadOnlyList<Archive> archives,
        Dictionary<
            string,
            Dictionary<string, HashSet<string>>
        > archivePartFileNamesByArchiveBaseNameByFolderPath,
        CancellationToken cancellationToken
    )
    {
        var newestArchiveIds = await repository.GetArchiveIdsThatAreNewestOfTheirArchiveConfigAsync(
            archives.Select(archive => archive.Id).ToList(),
            cancellationToken
        );

        var previewItems = archives
            .OrderBy(
                archive => archive.ArchiveConfig.Release.Name,
                StringComparer.OrdinalIgnoreCase
            )
            .ThenBy(archive => archive.Id)
            .Select(archive =>
                CreatePreviewItem(
                    archive: archive,
                    isNewestArchiveOfArchiveConfig: newestArchiveIds.Contains(archive.Id),
                    archivePartFileNamesByArchiveBaseNameByFolderPath: archivePartFileNamesByArchiveBaseNameByFolderPath
                )
            )
            .ToList();

        var folderPathsFoundForSeveralArchives = previewItems
            .Where(item => item.IsRelocatable)
            .GroupBy(item => item.FoundArchiveFolderPath!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var foundFolderPaths = previewItems
            .Where(item => item.IsRelocatable)
            .Select(item => item.FoundArchiveFolderPath!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var folderPathsUsedByCreatedArchives =
            await repository.GetFolderPathsUsedByCreatedArchivesAsync(
                foundFolderPaths,
                cancellationToken
            );

        return previewItems
            .Select(item =>
                ApplyFolderConflicts(
                    previewItem: item,
                    folderPathsFoundForSeveralArchives: folderPathsFoundForSeveralArchives,
                    folderPathsUsedByCreatedArchives: folderPathsUsedByCreatedArchives
                )
            )
            .ToList();
    }

    private static ArchiveFolderRelocationPreviewItem ApplyFolderConflicts(
        ArchiveFolderRelocationPreviewItem previewItem,
        HashSet<string> folderPathsFoundForSeveralArchives,
        HashSet<string> folderPathsUsedByCreatedArchives
    )
    {
        if (!previewItem.IsRelocatable)
        {
            return previewItem;
        }

        if (folderPathsFoundForSeveralArchives.Contains(previewItem.FoundArchiveFolderPath!))
        {
            return previewItem with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.MatchingFolderFoundForSeveralSelectedArchives,
            };
        }

        if (folderPathsUsedByCreatedArchives.Contains(previewItem.FoundArchiveFolderPath!))
        {
            return previewItem with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.MatchingFolderUsedByCreatedArchive,
            };
        }

        return previewItem;
    }

    private static ArchiveFolderRelocationPreviewItem CreatePreviewItem(
        Archive archive,
        bool isNewestArchiveOfArchiveConfig,
        Dictionary<
            string,
            Dictionary<string, HashSet<string>>
        > archivePartFileNamesByArchiveBaseNameByFolderPath
    )
    {
        var previewItem = new ArchiveFolderRelocationPreviewItem(
            ArchiveId: archive.Id,
            ReleaseName: archive.ArchiveConfig.Release.Name,
            ArchiveConfigName: archive.ArchiveConfig.Name,
            CurrentArchiveFolderPath: archive.ArchiveFolderPath,
            FoundArchiveFolderPath: null,
            NotRelocatableReason: null
        );

        if (archive.ArchiveState is not (ArchiveState.Deleted or ArchiveState.MissingFiles))
        {
            return previewItem with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.ArchiveIsNotDeletedOrMissingFiles,
            };
        }

        if (!isNewestArchiveOfArchiveConfig)
        {
            return previewItem with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.NewerArchiveExistsInArchiveConfig,
            };
        }

        var archiveFileNames = archive
            .ArchiveFiles.Select(archiveFile => Path.GetFileName(archiveFile.FullFileName))
            .ToList();

        var archiveBaseName = GetArchiveBaseName(archive);

        var matchingFolderPaths = archivePartFileNamesByArchiveBaseNameByFolderPath
            .Where(entry =>
                entry.Value.TryGetValue(archiveBaseName, out var archivePartFileNames)
                && archivePartFileNames.SetEquals(archiveFileNames)
            )
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (matchingFolderPaths.Count == 0)
        {
            return previewItem with
            {
                NotRelocatableReason = ArchiveFolderNotRelocatableReason.NoMatchingFolderFound,
            };
        }

        if (matchingFolderPaths.Count > 1)
        {
            return previewItem with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.SeveralMatchingFoldersFound,
            };
        }

        var matchingFolderPath = matchingFolderPaths.Single();

        var previewItemWithFoundFolder = previewItem with
        {
            FoundArchiveFolderPath = matchingFolderPath,
        };

        if (archivePartFileNamesByArchiveBaseNameByFolderPath[matchingFolderPath].Count > 1)
        {
            return previewItemWithFoundFolder with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.MatchingFolderContainsOtherArchive,
            };
        }

        if (
            IsInsideReleaseFolderOfManagedRelease(archive.ArchiveConfig.Release, matchingFolderPath)
        )
        {
            return previewItemWithFoundFolder with
            {
                NotRelocatableReason =
                    ArchiveFolderNotRelocatableReason.MatchingFolderInsideReleaseFolderOfManagedRelease,
            };
        }

        return previewItemWithFoundFolder;
    }

    private static bool IsInsideReleaseFolderOfManagedRelease(Release release, string folderPath)
    {
        return release.ReleaseType is ReleaseType.Managed
            && FolderPathHelper.IsSameOrSubPath(folderPath, release.ReleaseFolderPath);
    }

    private Dictionary<
        string,
        Dictionary<string, HashSet<string>>
    > GetArchivePartFileNamesByArchiveBaseNameByFolderPath(string basePath)
    {
        if (!fileSystemService.DirectoryExists(basePath))
        {
            throw new InvalidOperationException($"Base path {basePath} does not exist.");
        }

        return fileSystemService
            .GetFilesInPath(basePath, recursive: true)
            .Where(filePath => ArchivePartFileNames.IsArchivePart(Path.GetFileName(filePath)))
            .GroupBy(filePath => Path.GetDirectoryName(filePath)!, StringComparer.Ordinal)
            .ToDictionary(
                folderGroup => folderGroup.Key,
                folderGroup => GroupArchivePartFileNamesByArchiveBaseName(folderGroup.ToList()),
                StringComparer.Ordinal
            );
    }

    private static string GetArchiveBaseName(Archive archive)
    {
        return ArchivePartFileNames.GetArchiveBaseName(
            Path.GetFileName(archive.ArchiveFiles.First().FullFileName)
        )!;
    }

    private static Dictionary<string, HashSet<string>> GroupArchivePartFileNamesByArchiveBaseName(
        List<string> archivePartFilePaths
    )
    {
        return archivePartFilePaths
            .Select(filePath => Path.GetFileName(filePath))
            .GroupBy(
                fileName => ArchivePartFileNames.GetArchiveBaseName(fileName)!,
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                archiveBaseNameGroup => archiveBaseNameGroup.Key,
                archiveBaseNameGroup =>
                    archiveBaseNameGroup.ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase
            );
    }
}
