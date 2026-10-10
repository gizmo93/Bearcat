using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.UnmanagedReleases;

public class UnmanagedReleaseArchiveInitializationService(IArchiverFactory archiverFactory)
{
    public ArchiveConfig CreateArchiveConfig(
        Release release,
        string archiveFolderPath,
        DateTime createdAt
    )
    {
        if (release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Initial unmanaged archive configs can only be created for unmanaged releases."
            );
        }

        var archiverWithArchiveFiles = FindSingleArchiverWithArchiveFiles(archiveFolderPath);
        var archiver = archiverWithArchiveFiles.Archiver;

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
                    archiverWithArchiveFiles.ArchiveFilePaths,
                    createdAt
                ),
            ],
        };
    }

    public ArchiveFolderChangeResult ApplyArchiveFolder(
        ArchiveConfig archiveConfig,
        string archiveFolderPath,
        DateTime createdAt,
        bool confirmContentChange,
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders
    )
    {
        if (archiveConfig.Release.ReleaseType is not ReleaseType.Unmanaged)
        {
            throw new InvalidOperationException(
                "Archive folders can only be changed for unmanaged releases."
            );
        }

        var archiveFiles = FindArchiveFilesOfArchiver(archiveConfig, archiveFolderPath);
        var currentArchive = GetCurrentArchive(archiveConfig);

        var archiveStorageFolder = FindArchiveStorageFolderContainingPath(
            archiveStorageFolders,
            archiveFolderPath
        );

        if (currentArchive is not null && ArchiveFileNamesMatch(currentArchive, archiveFiles))
        {
            archiveConfig.ArchiveFilesBasePath = archiveFolderPath;
            RepointArchive(currentArchive, archiveFolderPath);
            AssignArchiveStorageFolder(currentArchive, archiveStorageFolder);
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

        var reimportedArchive = BuildArchive(archiveFolderPath, archiveFiles, createdAt);
        AssignArchiveStorageFolder(reimportedArchive, archiveStorageFolder);
        archiveConfig.Archives.Add(reimportedArchive);

        return ArchiveFolderChangeResult.Reimported;
    }

    private static ArchiveStorageFolder? FindArchiveStorageFolderContainingPath(
        IReadOnlyList<ArchiveStorageFolder> archiveStorageFolders,
        string archiveFolderPath
    )
    {
        return archiveStorageFolders
            .Where(storageFolder =>
                FolderPathHelper.IsSameOrSubPath(archiveFolderPath, storageFolder.Path)
            )
            .MaxBy(storageFolder => storageFolder.Path.Length);
    }

    private static void AssignArchiveStorageFolder(
        Archive archive,
        ArchiveStorageFolder? archiveStorageFolder
    )
    {
        archive.ArchiveStorageFolder = archiveStorageFolder;
        archive.ArchiveStorageFolderId = archiveStorageFolder?.Id;

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.Md5HashInStorageFolder = archiveStorageFolder is null
                ? null
                : archiveFile.Md5Hash;
        }
    }

    private static Archive BuildArchive(
        string archiveFolderPath,
        List<string> archiveFilePaths,
        DateTime createdAt
    )
    {
        return new Archive
        {
            ArchiveFolderPath = archiveFolderPath,
            CreatedAt = createdAt,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 0,
            ArchiveFiles = archiveFilePaths
                .Select(filePath => new ArchiveFile { FullFileName = filePath })
                .ToList(),
            Uploads = [],
            ErrorMessages = [],
            Notifications = [],
        };
    }

    private ArchiverWithArchiveFiles FindSingleArchiverWithArchiveFiles(string archiveFolderPath)
    {
        EnsureArchiveFolderExists(archiveFolderPath);

        var archiversWithArchiveFiles = archiverFactory
            .GetArchivers()
            .Select(archiver => new ArchiverWithArchiveFiles(
                archiver,
                FindArchiveFiles(archiveFolderPath, archiver.ClassName)
            ))
            .Where(archiverWithArchiveFiles => archiverWithArchiveFiles.ArchiveFilePaths.Count > 0)
            .ToList();

        if (archiversWithArchiveFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not contain supported archive files."
            );
        }

        if (archiversWithArchiveFiles.Count > 1)
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} contains archive files for multiple archivers."
            );
        }

        return archiversWithArchiveFiles[0];
    }

    private List<string> FindArchiveFilesOfArchiver(
        ArchiveConfig archiveConfig,
        string archiveFolderPath
    )
    {
        EnsureArchiveFolderExists(archiveFolderPath);

        var archiveFiles = FindArchiveFiles(archiveFolderPath, archiveConfig.ArchiverName);

        if (archiveFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not contain archive files for archiver {archiveConfig.Name}."
            );
        }

        return archiveFiles;
    }

    private List<string> FindArchiveFiles(string archiveFolderPath, string archiverClassName)
    {
        return archiverFactory
            .GetArchiveExtractorByName(archiverClassName)
            .FindArchivesToExtract(archiveFolderPath)
            .SelectMany(archive => archive.VolumeFilePaths)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureArchiveFolderExists(string archiveFolderPath)
    {
        if (!Directory.Exists(archiveFolderPath))
        {
            throw new InvalidOperationException(
                $"Archive folder path {archiveFolderPath} does not exist."
            );
        }
    }

    private static Archive? GetCurrentArchive(ArchiveConfig archiveConfig)
    {
        return archiveConfig
            .Archives.Where(archive => archive.ArchiveState is ArchiveState.Created)
            .OrderByDescending(archive => archive.CreatedAt)
            .ThenByDescending(archive => archive.Id)
            .FirstOrDefault();
    }

    private static bool ArchiveFileNamesMatch(Archive archive, List<string> archiveFilePaths)
    {
        var currentFileNames = archive
            .ArchiveFiles.Select(file => Path.GetFileName(file.FullFileName))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        var discoveredFileNames = archiveFilePaths
            .Select(Path.GetFileName)
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        return currentFileNames.SequenceEqual(
            discoveredFileNames,
            StringComparer.OrdinalIgnoreCase
        );
    }

    private static void RepointArchive(Archive archive, string archiveFolderPath)
    {
        foreach (var archiveFile in ArchiveLocalWorkingCopyFiles.GetLocalWorkingCopyFiles(archive))
        {
            archiveFile.Md5Hash = archiveFile.Md5HashInStorageFolder;
        }

        archive.ArchiveFolderPath = archiveFolderPath;

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.FullFileName = Path.Combine(
                archiveFolderPath,
                Path.GetFileName(archiveFile.FullFileName)
            );
        }
    }

    private sealed record ArchiverWithArchiveFiles(
        ArchiverDto Archiver,
        List<string> ArchiveFilePaths
    );
}
