using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.SfvVerification;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Extraction;

public class DownloadFolderArchiveExtractionService(
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    ILogger<DownloadFolderArchiveExtractionService> logger
)
{
    public const string TemporaryExtractionFolderName = ".bearcat-extraction";

    public async Task<IReadOnlyList<ArchiveToExtract>> ExtractAllArchivesAsync(
        string localFolderPath,
        IReadOnlyList<SfvFile> sfvFiles,
        CancellationToken cancellationToken
    )
    {
        var archives = FindArchivesInFolderAndSubfolders(localFolderPath);

        if (archives.Count == 0)
        {
            return [];
        }

        VerifyEnoughFreeDiskSpace(localFolderPath, archives);

        var extractedArchives = new List<ExtractedArchive>();

        foreach (var (archive, index) in archives.Select((archive, index) => (archive, index)))
        {
            extractedArchives.Add(
                await ExtractIntoTemporaryFolderAsync(
                    localFolderPath: localFolderPath,
                    archive: archive,
                    runningNumber: index + 1,
                    cancellationToken: cancellationToken
                )
            );
        }

        var entriesToMove = GetEntriesToMove(extractedArchives);

        VerifyNoTargetPathConflicts(localFolderPath, entriesToMove);
        MoveEntries(entriesToMove);
        DeleteTemporaryExtractionFoldersOfArchiveFolders(extractedArchives);

        var deletedVolumeFilePaths = DeleteVolumes(archives);
        DeleteSfvFilesCoveringOnlyDeletedVolumes(localFolderPath, sfvFiles, deletedVolumeFilePaths);

        return archives.Select(archive => archive.ArchiveToExtract).ToList();
    }

    public void DeleteTemporaryExtractionFolders(string localFolderPath)
    {
        fileSystemService.DeleteDirectoriesByNameRecursively(
            localFolderPath,
            TemporaryExtractionFolderName
        );
    }

    private List<ArchiveWithExtractor> FindArchivesInFolderAndSubfolders(string localFolderPath)
    {
        var extractors = archiverFactory.GetArchiveExtractors();

        return GetFolderAndSubfolders(localFolderPath)
            .SelectMany(folderPath =>
                extractors.SelectMany(extractor =>
                    extractor
                        .FindArchivesToExtract(folderPath)
                        .Select(archive => new ArchiveWithExtractor(archive, extractor))
                )
            )
            .ToList();
    }

    private static List<string> GetFolderAndSubfolders(string folderPath)
    {
        var folderPaths = new List<string> { folderPath };

        foreach (
            var subfolderPath in Directory.GetDirectories(folderPath).Order(StringComparer.Ordinal)
        )
        {
            if (Path.GetFileName(subfolderPath) == TemporaryExtractionFolderName)
            {
                continue;
            }

            folderPaths.AddRange(GetFolderAndSubfolders(subfolderPath));
        }

        return folderPaths;
    }

    private static void VerifyEnoughFreeDiskSpace(
        string localFolderPath,
        List<ArchiveWithExtractor> archives
    )
    {
        var requiredBytes = archives
            .SelectMany(archive => archive.ArchiveToExtract.VolumeFilePaths)
            .Sum(volumeFilePath => new FileInfo(volumeFilePath).Length);

        var availableBytes = new DriveInfo(localFolderPath).AvailableFreeSpace;

        if (requiredBytes > availableBytes)
        {
            throw new IOException(
                $"Extracting the archives needs up to {requiredBytes.Bytes().Humanize()} of free disk space, but only {availableBytes.Bytes().Humanize()} are available for {localFolderPath}"
            );
        }
    }

    private async Task<ExtractedArchive> ExtractIntoTemporaryFolderAsync(
        string localFolderPath,
        ArchiveWithExtractor archive,
        int runningNumber,
        CancellationToken cancellationToken
    )
    {
        var firstVolumeFilePath = archive.ArchiveToExtract.FirstVolumeFilePath;
        var archiveFolderPath = Path.GetDirectoryName(firstVolumeFilePath)!;

        var temporaryFolderPath = Path.Join(
            archiveFolderPath,
            TemporaryExtractionFolderName,
            runningNumber.ToString(CultureInfo.InvariantCulture)
        );

        if (Directory.Exists(temporaryFolderPath))
        {
            Directory.Delete(temporaryFolderPath, recursive: true);
        }

        Directory.CreateDirectory(temporaryFolderPath);

        logger.LogInformation(
            "Extracting {FirstVolumeFilePath} with {VolumeCount} volumes into {TemporaryFolderPath}",
            firstVolumeFilePath,
            archive.ArchiveToExtract.VolumeFilePaths.Count,
            temporaryFolderPath
        );

        var result = await archive.Extractor.ExtractAsync(
            archiveToExtract: archive.ArchiveToExtract,
            destinationFolderPath: temporaryFolderPath,
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            throw new IOException(
                $"Extracting '{Path.GetRelativePath(localFolderPath, firstVolumeFilePath)}' failed: {string.Join(" | ", result.ErrorMessages)}"
            );
        }

        return new ExtractedArchive(archiveFolderPath, temporaryFolderPath);
    }

    private static List<EntryToMove> GetEntriesToMove(List<ExtractedArchive> extractedArchives)
    {
        return extractedArchives
            .SelectMany(extractedArchive =>
                Directory
                    .GetFileSystemEntries(extractedArchive.TemporaryFolderPath)
                    .Order(StringComparer.Ordinal)
                    .Select(entryPath => new EntryToMove(
                        SourcePath: entryPath,
                        TargetPath: Path.Join(
                            extractedArchive.ArchiveFolderPath,
                            Path.GetFileName(entryPath)
                        )
                    ))
            )
            .ToList();
    }

    private static void VerifyNoTargetPathConflicts(
        string localFolderPath,
        List<EntryToMove> entriesToMove
    )
    {
        var conflictingTargetPaths = entriesToMove
            .GroupBy(entry => entry.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1 || Path.Exists(group.Key))
            .Select(group => Path.GetRelativePath(localFolderPath, group.Key))
            .ToList();

        if (conflictingTargetPaths.Count > 0)
        {
            throw new IOException(
                $"The extracted entries {string.Join(", ", conflictingTargetPaths)} already exist in the download folder or are contained in more than one archive, so nothing was moved"
            );
        }
    }

    private static void MoveEntries(List<EntryToMove> entriesToMove)
    {
        foreach (var entry in entriesToMove)
        {
            if (Directory.Exists(entry.SourcePath))
            {
                Directory.Move(entry.SourcePath, entry.TargetPath);
            }
            else
            {
                File.Move(entry.SourcePath, entry.TargetPath);
            }
        }
    }

    private static void DeleteTemporaryExtractionFoldersOfArchiveFolders(
        List<ExtractedArchive> extractedArchives
    )
    {
        foreach (
            var archiveFolderPath in extractedArchives
                .Select(extractedArchive => extractedArchive.ArchiveFolderPath)
                .Distinct(StringComparer.Ordinal)
        )
        {
            Directory.Delete(
                Path.Join(archiveFolderPath, TemporaryExtractionFolderName),
                recursive: true
            );
        }
    }

    private static HashSet<string> DeleteVolumes(List<ArchiveWithExtractor> archives)
    {
        var deletedVolumeFilePaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (
            var volumeFilePath in archives.SelectMany(archive =>
                archive.ArchiveToExtract.VolumeFilePaths
            )
        )
        {
            File.Delete(volumeFilePath);
            deletedVolumeFilePaths.Add(Path.GetFullPath(volumeFilePath));
        }

        return deletedVolumeFilePaths;
    }

    private void DeleteSfvFilesCoveringOnlyDeletedVolumes(
        string localFolderPath,
        IReadOnlyList<SfvFile> sfvFiles,
        HashSet<string> deletedVolumeFilePaths
    )
    {
        var sfvFilePathsToDelete = sfvFiles
            .Where(sfvFile =>
                sfvFile.Entries.Count > 0
                && sfvFile.Entries.All(entry => deletedVolumeFilePaths.Contains(entry.FilePath))
            )
            .Select(sfvFile => sfvFile.FilePath)
            .ToList();

        foreach (var sfvFilePath in sfvFilePathsToDelete)
        {
            File.Delete(sfvFilePath);

            logger.LogInformation(
                "Deleted the SFV file {SfvFilePath} in {LocalFolderPath} because it only lists extracted archive volumes",
                Path.GetRelativePath(localFolderPath, sfvFilePath),
                localFolderPath
            );
        }
    }

    private sealed record ExtractedArchive(string ArchiveFolderPath, string TemporaryFolderPath);

    private sealed record EntryToMove(string SourcePath, string TargetPath);

    private sealed record ArchiveWithExtractor(
        ArchiveToExtract ArchiveToExtract,
        IArchiveExtractor Extractor
    );
}
