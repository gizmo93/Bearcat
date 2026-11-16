using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public class ArchivePartMover(
    IFileSystemService fileSystemService,
    ILogger<ArchivePartMover> logger
)
{
    public void MoveArchiveParts(IReadOnlyList<ArchivePartsMove> archivePartsMoves)
    {
        var targetFolderPathWithEntries = archivePartsMoves
            .Select(archivePartsMove => archivePartsMove.TargetFolderPath)
            .FirstOrDefault(fileSystemService.DirectoryHasEntries);

        if (targetFolderPathWithEntries is not null)
        {
            throw new InvalidLinkCrypterImportException(
                $"The archive parts cannot be moved into folder {targetFolderPathWithEntries} because it is not empty."
            );
        }

        foreach (var archivePartsMove in archivePartsMoves)
        {
            fileSystemService.CreateDirectory(archivePartsMove.TargetFolderPath);

            foreach (var sourceFilePath in archivePartsMove.SourceFilePaths)
            {
                fileSystemService.MoveFile(
                    sourceFilePath,
                    Path.Combine(
                        archivePartsMove.TargetFolderPath,
                        Path.GetFileName(sourceFilePath)
                    )
                );
            }

            logger.LogInformation(
                "Moved {FileCount} archive parts of release {ReleaseName} into folder {TargetFolderPath}",
                archivePartsMove.SourceFilePaths.Count,
                archivePartsMove.ReleaseName,
                archivePartsMove.TargetFolderPath
            );
        }
    }
}
