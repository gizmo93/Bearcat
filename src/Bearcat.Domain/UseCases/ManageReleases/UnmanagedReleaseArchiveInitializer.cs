using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases;

public static class UnmanagedReleaseArchiveInitializer
{
    public static ArchiveConfig CreateArchiveConfig(
        Release release,
        IReadOnlyList<ArchiverDto> archivers,
        DateTime createdAt
    )
    {
        if (release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Initial unmanaged archive configs can only be created for unmanaged releases."
            );
        }

        var archiveFiles = GetArchiveFiles(release.ReleaseFolderPath, archivers);
        var archiver = archiveFiles
            .Select(file => file.Archiver)
            .DistinctBy(a => a.ClassName)
            .Single();

        return new ArchiveConfig
        {
            Release = release,
            Name = archiveFiles[0].Archiver.Name,
            ArchiveFilesBasePath = release.ReleaseFolderPath,
            ArchiverName = archiver.ClassName,
            ArchiveNamePrefix = null,
            ArchivePassword = null,
            ArchiveFileSizeMb = 0,
            UploadConfigs = [],
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = release.ReleaseFolderPath,
                    CreatedAt = createdAt,
                    ArchiveState = ArchiveState.Created,
                    ArchiveFileSizeMb = 0,
                    ArchiveFiles = archiveFiles
                        .Select(file => new ArchiveFile { FullFileName = file.FullFileName })
                        .ToList(),
                    Uploads = [],
                    ErrorMessages = [],
                    Notifications = [],
                },
            ],
        };
    }

    public static void RefreshArchiveConfig(
        ArchiveConfig archiveConfig,
        IReadOnlyList<ArchiverDto> archivers,
        DateTime createdAt
    )
    {
        if (archiveConfig.Release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Unmanaged archive refresh can only be used for unmanaged releases."
            );
        }

        var archiver = archivers.SingleOrDefault(archiver =>
            string.Equals(archiver.ClassName, archiveConfig.ArchiverName, StringComparison.Ordinal)
        );

        if (archiver is null)
        {
            throw new InvalidOperationException(
                $"Archiver {archiveConfig.ArchiverName} is no longer available."
            );
        }

        var releaseFolderPath = archiveConfig.Release.ReleaseFolderPath;
        var currentArchive = GetCurrentArchive(archiveConfig);
        var archiveFiles = GetArchiveFiles(releaseFolderPath, archiver);

        if (currentArchive is not null && ArchiveFileNamesMatch(currentArchive, archiveFiles))
        {
            archiveConfig.ArchiveFilesBasePath = releaseFolderPath;
            RepointArchive(currentArchive, releaseFolderPath);
            return;
        }

        archiveConfig.ArchiveFilesBasePath = releaseFolderPath;

        foreach (
            var archive in archiveConfig.Archives.Where(archive =>
                archive.ArchiveState is ArchiveState.Created
            )
        )
        {
            archive.ArchiveState = ArchiveState.Deleted;
        }

        archiveConfig.Archives.Add(
            new Archive
            {
                ArchiveFolderPath = releaseFolderPath,
                CreatedAt = createdAt,
                ArchiveState = ArchiveState.Created,
                ArchiveFileSizeMb = 0,
                ArchiveFiles = archiveFiles
                    .Select(file => new ArchiveFile { FullFileName = file.FullFileName })
                    .ToList(),
                Uploads = [],
                ErrorMessages = [],
                Notifications = [],
            }
        );
    }

    private static IReadOnlyList<UnmanagedArchiveFile> GetArchiveFiles(
        string releaseFolderPath,
        IReadOnlyList<ArchiverDto> archivers
    )
    {
        if (!Directory.Exists(releaseFolderPath))
        {
            throw new InvalidOperationException(
                $"Release folder path {releaseFolderPath} does not exist."
            );
        }

        var archiveFiles = Directory
            .EnumerateFiles(releaseFolderPath)
            .Select(file => new
            {
                FullFileName = file,
                MatchingArchivers = archivers
                    .Where(archiver => FileMatchesArchiver(file, archiver))
                    .ToList(),
            })
            .Where(file => file.MatchingArchivers.Count > 0)
            .Select(file => new UnmanagedArchiveFile(
                file.FullFileName,
                file.MatchingArchivers.Single()
            ))
            .OrderBy(file => file.FullFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archiveFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Release folder path {releaseFolderPath} does not contain supported archive files."
            );
        }

        var matchingArchivers = archiveFiles
            .Select(file => file.Archiver)
            .DistinctBy(archiver => archiver.ClassName)
            .ToList();

        if (matchingArchivers.Count > 1)
        {
            throw new InvalidOperationException(
                $"Release folder path {releaseFolderPath} contains archive files for multiple archivers."
            );
        }

        return archiveFiles;
    }

    private static IReadOnlyList<UnmanagedArchiveFile> GetArchiveFiles(
        string releaseFolderPath,
        ArchiverDto archiver
    )
    {
        if (!Directory.Exists(releaseFolderPath))
        {
            throw new InvalidOperationException(
                $"Release folder path {releaseFolderPath} does not exist."
            );
        }

        var archiveFiles = Directory
            .EnumerateFiles(releaseFolderPath)
            .Where(file => FileMatchesArchiver(file, archiver))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(file => new UnmanagedArchiveFile(file, archiver))
            .ToList();

        if (archiveFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Release folder path {releaseFolderPath} does not contain archive files for archiver {archiver.Name}."
            );
        }

        return archiveFiles;
    }

    private static Archive? GetCurrentArchive(ArchiveConfig archiveConfig)
    {
        return archiveConfig
            .Archives.Where(archive => archive.ArchiveState is ArchiveState.Created)
            .OrderByDescending(archive => archive.CreatedAt)
            .ThenByDescending(archive => archive.Id)
            .FirstOrDefault();
    }

    private static bool ArchiveFileNamesMatch(
        Archive archive,
        IReadOnlyList<UnmanagedArchiveFile> archiveFiles
    )
    {
        var currentFileNames = archive
            .ArchiveFiles.Select(file => Path.GetFileName(file.FullFileName))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);
        var discoveredFileNames = archiveFiles
            .Select(file => Path.GetFileName(file.FullFileName))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        return currentFileNames.SequenceEqual(
            discoveredFileNames,
            StringComparer.OrdinalIgnoreCase
        );
    }

    private static void RepointArchive(Archive archive, string releaseFolderPath)
    {
        archive.ArchiveFolderPath = releaseFolderPath;

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.FullFileName = Path.Combine(
                releaseFolderPath,
                Path.GetFileName(archiveFile.FullFileName)
            );
        }
    }

    private static bool FileMatchesArchiver(string filePath, ArchiverDto archiver)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.EndsWith(archiver.FileExtension, StringComparison.OrdinalIgnoreCase)
            || fileName.Contains($"{archiver.FileExtension}.", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record UnmanagedArchiveFile(string FullFileName, ArchiverDto Archiver);
}
