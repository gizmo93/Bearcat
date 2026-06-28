using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases;

public static class UnmanagedReleaseArchiveInitializer
{
    public static ArchiveConfig CreateArchiveConfig(
        Release release,
        string archiveFolderPath,
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

        var archiveFiles = GetArchiveFiles(archiveFolderPath, archivers);
        var archiver = archiveFiles
            .Select(file => file.Archiver)
            .DistinctBy(a => a.ClassName)
            .Single();

        return new ArchiveConfig
        {
            Release = release,
            Name = archiver.Name,
            ArchiveFilesBasePath = archiveFolderPath,
            ArchiverName = archiver.ClassName,
            ArchiveNamePrefix = null,
            ArchivePassword = null,
            ArchiveFileSizeMb = 0,
            UploadConfigs = [],
            Archives =
            [
                BuildArchive(
                    archiveFolderPath,
                    archiveFiles.Select(file => file.FullFileName).ToList(),
                    createdAt
                ),
            ],
        };
    }

    public static ArchiveFolderChangeResult ApplyArchiveFolder(
        ArchiveConfig archiveConfig,
        string archiveFolderPath,
        IArchiver archiver,
        DateTime createdAt,
        bool confirmContentChange
    )
    {
        if (archiveConfig.Release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Archive folders can only be changed for unmanaged releases."
            );
        }

        var archiveFiles = GetArchiveFiles(archiveFolderPath, archiver);
        var currentArchive = GetCurrentArchive(archiveConfig);

        if (currentArchive is not null && ArchiveFileNamesMatch(currentArchive, archiveFiles))
        {
            archiveConfig.ArchiveFilesBasePath = archiveFolderPath;
            RepointArchive(currentArchive, archiveFolderPath);
            return ArchiveFolderChangeResult.Relocated;
        }

        if (!confirmContentChange)
        {
            return ArchiveFolderChangeResult.ConfirmationRequired;
        }

        archiveConfig.ArchiveFilesBasePath = archiveFolderPath;

        foreach (
            var archive in archiveConfig.Archives.Where(archive =>
                archive.ArchiveState is ArchiveState.Created
            )
        )
        {
            archive.ArchiveState = ArchiveState.Deleted;
        }

        archiveConfig.Archives.Add(BuildArchive(archiveFolderPath, archiveFiles, createdAt));

        return ArchiveFolderChangeResult.Reimported;
    }

    private static Archive BuildArchive(
        string archiveFolderPath,
        IReadOnlyList<string> archiveFileNames,
        DateTime createdAt
    )
    {
        return new Archive
        {
            ArchiveFolderPath = archiveFolderPath,
            CreatedAt = createdAt,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 0,
            ArchiveFiles = archiveFileNames
                .Select(fileName => new ArchiveFile { FullFileName = fileName })
                .ToList(),
            Uploads = [],
            ErrorMessages = [],
            Notifications = [],
        };
    }

    private static IReadOnlyList<UnmanagedArchiveFile> GetArchiveFiles(
        string archiveFolderPath,
        IReadOnlyList<ArchiverDto> archivers
    )
    {
        if (!Directory.Exists(archiveFolderPath))
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not exist."
            );
        }

        var archiveFiles = Directory
            .EnumerateFiles(archiveFolderPath)
            .Select(file => new
            {
                FullFileName = file,
                MatchingArchivers = archivers
                    .Where(archiver => FileMatchesArchiver(file, archiver.FileExtension))
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
                $"Archive folder path {archiveFolderPath} does not contain supported archive files."
            );
        }

        var matchingArchivers = archiveFiles
            .Select(file => file.Archiver)
            .DistinctBy(archiver => archiver.ClassName)
            .ToList();

        if (matchingArchivers.Count > 1)
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} contains archive files for multiple archivers."
            );
        }

        return archiveFiles;
    }

    private static IReadOnlyList<string> GetArchiveFiles(
        string archiveFolderPath,
        IArchiver archiver
    )
    {
        if (!Directory.Exists(archiveFolderPath))
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not exist."
            );
        }

        var archiveFiles = Directory
            .EnumerateFiles(archiveFolderPath)
            .Where(file => FileMatchesArchiver(file, archiver.FileExtension))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (archiveFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not contain archive files for archiver {archiver.Name}."
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
        IReadOnlyList<string> archiveFileNames
    )
    {
        var currentFileNames = archive
            .ArchiveFiles.Select(file => Path.GetFileName(file.FullFileName))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        var discoveredFileNames = archiveFileNames
            .Select(Path.GetFileName)
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        return currentFileNames.SequenceEqual(
            discoveredFileNames,
            StringComparer.OrdinalIgnoreCase
        );
    }

    private static void RepointArchive(Archive archive, string archiveFolderPath)
    {
        archive.ArchiveFolderPath = archiveFolderPath;

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.FullFileName = Path.Combine(
                archiveFolderPath,
                Path.GetFileName(archiveFile.FullFileName)
            );
        }
    }

    private static bool FileMatchesArchiver(string filePath, string fileExtension)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase)
            || fileName.Contains($"{fileExtension}.", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record UnmanagedArchiveFile(string FullFileName, ArchiverDto Archiver);
}
