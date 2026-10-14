using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;

public class ArchiveFileCopyService(
    IFileSystemService fileSystemService,
    ITransferProgressTracker progressTracker,
    ILogger<ArchiveFileCopyService> logger
)
{
    public List<ArchiveFileWithSize> GetArchiveFilesWithSize(
        IReadOnlyList<ArchiveFile> archiveFiles
    )
    {
        return archiveFiles
            .Select(file => new ArchiveFileWithSize(
                file,
                fileSystemService.GetFileSizeBytes(file.FullFileName)
            ))
            .ToList();
    }

    public async Task<IReadOnlyList<CopiedArchiveFile>> CopyArchiveFilesAndVerifySizesAsync(
        int archiveId,
        IReadOnlyList<ArchiveFileWithSize> archiveFilesWithSize,
        string targetFolderPath,
        string sourceName,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        fileSystemService.CreateDirectory(targetFolderPath);

        progressTracker.StartTracking(
            identifier: transferIdentifier,
            plannedFiles: archiveFilesWithSize
                .Select(file => new TransferFile(
                    FileId: file.ArchiveFile.Id,
                    FileName: Path.GetFileName(file.ArchiveFile.FullFileName),
                    SourceName: sourceName,
                    SizeBytes: file.SizeBytes,
                    IsAlreadyTransferred: false
                ))
                .ToList()
        );

        var copiedFiles = new List<CopiedArchiveFile>();

        try
        {
            foreach (var file in archiveFilesWithSize)
            {
                var fileName = Path.GetFileName(file.ArchiveFile.FullFileName);
                var targetFilePath = Path.Join(targetFolderPath, fileName);

                await fileSystemService.CopyFileAsync(
                    sourceFilePath: file.ArchiveFile.FullFileName,
                    destinationFilePath: targetFilePath,
                    progress: new TransferProgressReporter(
                        tracker: progressTracker,
                        identifier: transferIdentifier,
                        fileId: file.ArchiveFile.Id,
                        fileName: fileName,
                        sourceName: sourceName
                    ),
                    cancellationToken: cancellationToken
                );

                copiedFiles.Add(new CopiedArchiveFile(file.ArchiveFile, targetFilePath));

                var copiedSizeBytes = fileSystemService.GetFileSizeBytes(targetFilePath);

                if (copiedSizeBytes != file.SizeBytes)
                {
                    throw new IOException(
                        $"Copied file {targetFilePath} has {copiedSizeBytes} bytes instead of {file.SizeBytes} bytes"
                    );
                }
            }

            return copiedFiles;
        }
        catch
        {
            DeleteCopiedFiles(
                archiveId: archiveId,
                copiedFiles: copiedFiles,
                targetFolderPath: targetFolderPath
            );

            throw;
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }

    public void DeleteCopiedFiles(
        int archiveId,
        IReadOnlyList<CopiedArchiveFile> copiedFiles,
        string targetFolderPath
    )
    {
        try
        {
            foreach (var copiedFile in copiedFiles)
            {
                fileSystemService.DeleteFileIfExists(copiedFile.CopiedFilePath);
            }

            fileSystemService.DeleteDirectoryIfEmpty(targetFolderPath);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Could not delete the files copied to {TargetFolderPath} for archive {ArchiveId}",
                targetFolderPath,
                archiveId
            );
        }
    }
}
