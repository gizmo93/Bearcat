using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class ArchivePartMover(
    IFileSystemService fileSystemService,
    ILogger<ArchivePartMover> logger
)
{
    public void MoveArchiveParts(IReadOnlyList<ArchivePartsToMove> archivePartsToMovePerRelease)
    {
        var targetFolderPathWithEntries = archivePartsToMovePerRelease
            .Select(archivePartsToMove => archivePartsToMove.TargetFolderPath)
            .FirstOrDefault(fileSystemService.DirectoryHasEntries);

        if (targetFolderPathWithEntries is not null)
        {
            throw new InvalidLinkCrypterImportException(
                $"The archive parts cannot be moved into folder {targetFolderPathWithEntries} because it is not empty."
            );
        }

        foreach (var archivePartsToMove in archivePartsToMovePerRelease)
        {
            fileSystemService.CreateDirectory(archivePartsToMove.TargetFolderPath);

            foreach (var sourceFilePath in archivePartsToMove.SourceFilePaths)
            {
                fileSystemService.MoveFile(
                    sourceFilePath,
                    Path.Combine(
                        archivePartsToMove.TargetFolderPath,
                        Path.GetFileName(sourceFilePath)
                    )
                );
            }

            logger.LogInformation(
                "Moved {FileCount} archive parts of release {ReleaseName} into folder {TargetFolderPath}",
                archivePartsToMove.SourceFilePaths.Count,
                archivePartsToMove.ReleaseName,
                archivePartsToMove.TargetFolderPath
            );
        }
    }
}
