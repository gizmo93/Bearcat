using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.Extraction;

public class FolderArchiveExtractionService(
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    ITransferProgressTracker progressTracker,
    FolderSizeProgressReporter folderSizeProgressReporter,
    ILogger<FolderArchiveExtractionService> logger
)
{
    public const string TemporaryExtractionFolderName = ".bearcat-extraction";

    public async Task<IReadOnlyList<ArchiveToExtract>> ExtractAllArchivesAsync(
        string folderPath,
        IReadOnlyList<SfvFile> sfvFiles,
        TransferIdentifier transferIdentifier,
        string sourceName,
        CancellationToken cancellationToken
    )
    {
        var archives = FindArchivesInFolderAndSubfolders(folderPath);

        if (archives.Count == 0)
        {
            return [];
        }

        VerifyEnoughFreeDiskSpace(folderPath, archives);

        var plannedFiles = archives
            .Select(
                (archive, index) =>
                    new TransferFile(
                        FileId: index + 1,
                        FileName: Path.GetRelativePath(
                            folderPath,
                            archive.ArchiveToExtract.FirstVolumeFilePath
                        ),
                        SourceName: sourceName,
                        SizeBytes: GetTotalVolumeSizeBytes(archive),
                        IsAlreadyTransferred: false
                    )
            )
            .ToList();

        progressTracker.StartTracking(transferIdentifier, plannedFiles);

        List<ExtractedArchive> extractedArchives;

        try
        {
            extractedArchives = await ExtractIntoTemporaryFoldersAsync(
                folderPath: folderPath,
                archivesWithPlannedFiles: archives.Zip(plannedFiles).ToList(),
                transferIdentifier: transferIdentifier,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }

        var entriesToMove = GetEntriesToMove(extractedArchives);

        VerifyNoTargetPathConflicts(folderPath, entriesToMove);
        MoveEntries(entriesToMove);
        DeleteTemporaryExtractionFoldersOfArchiveFolders(extractedArchives);

        var deletedVolumeFilePaths = DeleteVolumes(archives);
        DeleteSfvFilesCoveringOnlyDeletedVolumes(folderPath, sfvFiles, deletedVolumeFilePaths);

        return archives.Select(archive => archive.ArchiveToExtract).ToList();
    }

    public void DeleteTemporaryExtractionFolders(string folderPath)
    {
        fileSystemService.DeleteDirectoriesByNameRecursively(
            folderPath,
            TemporaryExtractionFolderName
        );
    }

    private List<ArchiveWithExtractor> FindArchivesInFolderAndSubfolders(string folderPath)
    {
        var extractors = archiverFactory.GetArchiveExtractors();

        return GetFolderAndSubfolders(folderPath)
            .SelectMany(folderOrSubfolderPath =>
                extractors.SelectMany(extractor =>
                    extractor
                        .FindArchivesToExtract(folderOrSubfolderPath)
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
        string folderPath,
        List<ArchiveWithExtractor> archives
    )
    {
        var requiredBytes = archives.Sum(GetTotalVolumeSizeBytes);

        var availableBytes = new DriveInfo(folderPath).AvailableFreeSpace;

        if (requiredBytes > availableBytes)
        {
            throw new IOException(
                $"Extracting the archives needs up to {requiredBytes.Bytes().Humanize(CultureInfo.InvariantCulture)} of free disk space, but only {availableBytes.Bytes().Humanize(CultureInfo.InvariantCulture)} are available for {folderPath}"
            );
        }
    }

    private static long GetTotalVolumeSizeBytes(ArchiveWithExtractor archive)
    {
        return archive.ArchiveToExtract.VolumeFilePaths.Sum(volumeFilePath =>
            new FileInfo(volumeFilePath).Length
        );
    }

    private async Task<List<ExtractedArchive>> ExtractIntoTemporaryFoldersAsync(
        string folderPath,
        List<(ArchiveWithExtractor Archive, TransferFile PlannedFile)> archivesWithPlannedFiles,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        var extractedArchives = new List<ExtractedArchive>();

        foreach (var (archive, plannedFile) in archivesWithPlannedFiles)
        {
            var progress = new TransferProgressReporter(
                tracker: progressTracker,
                identifier: transferIdentifier,
                fileId: plannedFile.FileId,
                fileName: plannedFile.FileName,
                sourceName: plannedFile.SourceName
            );

            extractedArchives.Add(
                await ExtractIntoTemporaryFolderAsync(
                    folderPath: folderPath,
                    archive: archive,
                    runningNumber: plannedFile.FileId,
                    progress: progress,
                    cancellationToken: cancellationToken
                )
            );
        }

        return extractedArchives;
    }

    private async Task<ExtractedArchive> ExtractIntoTemporaryFolderAsync(
        string folderPath,
        ArchiveWithExtractor archive,
        int runningNumber,
        ITransferProgress progress,
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

        var result = await folderSizeProgressReporter.RunWhileReportingFolderSizeAsync(
            folderPath: temporaryFolderPath,
            progress: progress,
            operation: () =>
                archive.Extractor.ExtractAsync(
                    archiveToExtract: archive.ArchiveToExtract,
                    destinationFolderPath: temporaryFolderPath,
                    cancellationToken: cancellationToken
                ),
            cancellationToken: cancellationToken
        );

        if (!result.IsSuccess)
        {
            throw new IOException(
                $"Extracting '{Path.GetRelativePath(folderPath, firstVolumeFilePath)}' failed: {string.Join(" | ", result.ErrorMessages)}"
            );
        }

        return new ExtractedArchive(
            ArchiveFolderPath: archiveFolderPath,
            ExtractedEntriesFolderPath: GetFolderContainingExtractedEntries(
                folderPath,
                firstVolumeFilePath,
                temporaryFolderPath
            )
        );
    }

    private string GetFolderContainingExtractedEntries(
        string folderPath,
        string firstVolumeFilePath,
        string temporaryFolderPath
    )
    {
        var entryPaths = Directory.GetFileSystemEntries(temporaryFolderPath);

        if (entryPaths.Length != 1 || !Directory.Exists(entryPaths[0]))
        {
            return temporaryFolderPath;
        }

        logger.LogInformation(
            "Skipping the wrapper folder {WrapperFolderName} of {ArchiveFilePath} and moving its content instead",
            Path.GetFileName(entryPaths[0]),
            Path.GetRelativePath(folderPath, firstVolumeFilePath)
        );

        return entryPaths[0];
    }

    private static List<EntryToMove> GetEntriesToMove(List<ExtractedArchive> extractedArchives)
    {
        return extractedArchives
            .SelectMany(extractedArchive =>
                Directory
                    .GetFileSystemEntries(extractedArchive.ExtractedEntriesFolderPath)
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
        string folderPath,
        List<EntryToMove> entriesToMove
    )
    {
        var conflictingTargetPaths = entriesToMove
            .GroupBy(entry => entry.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1 || Path.Exists(group.Key))
            .Select(group => Path.GetRelativePath(folderPath, group.Key))
            .ToList();

        if (conflictingTargetPaths.Count > 0)
        {
            throw new IOException(
                $"The extracted entries {string.Join(", ", conflictingTargetPaths)} already exist in the folder or are contained in more than one archive, so nothing was moved"
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
        string folderPath,
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
                "Deleted the SFV file {SfvFilePath} in {FolderPath} because it only lists extracted archive volumes",
                Path.GetRelativePath(folderPath, sfvFilePath),
                folderPath
            );
        }
    }

    private sealed record ExtractedArchive(
        string ArchiveFolderPath,
        string ExtractedEntriesFolderPath
    );

    private sealed record EntryToMove(string SourcePath, string TargetPath);

    private sealed record ArchiveWithExtractor(
        ArchiveToExtract ArchiveToExtract,
        IArchiveExtractor Extractor
    );
}
