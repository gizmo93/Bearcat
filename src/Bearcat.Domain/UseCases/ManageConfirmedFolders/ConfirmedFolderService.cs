using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;
using Bearcat.Domain.UseCases.ManageConfirmedFolders.Repositories;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageConfirmedFolders;

public class ConfirmedFolderService(
    ConfirmableFolderRootProvider rootProvider,
    FolderConfirmationCheck folderConfirmationCheck,
    IConfirmedFolderWriteRepository writeRepository,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    ILogger<ConfirmedFolderService> logger
)
{
    public const int MaxTopLevelEntryCount = 50;

    public async Task<IReadOnlyList<ConfirmableFolderReadModel>> GetConfirmableFoldersAsync(
        CancellationToken cancellationToken
    )
    {
        var confirmableFolders = new List<ConfirmableFolderReadModel>();

        foreach (var rootPath in rootProvider.GetRootPaths())
        {
            var result = await folderConfirmationCheck.GetFolderConfirmationAsync(
                rootPath,
                cancellationToken
            );

            confirmableFolders.Add(
                new ConfirmableFolderReadModel(
                    Path: rootPath,
                    State: result.State,
                    Exists: fileSystemService.DirectoryExists(rootPath)
                )
            );
        }

        return confirmableFolders;
    }

    public FolderTopLevelEntriesReadModel GetTopLevelEntries(string rootPath)
    {
        EnsureIsConfirmableRootPath(rootPath);

        if (!fileSystemService.DirectoryExists(rootPath))
        {
            return new FolderTopLevelEntriesReadModel(Entries: [], TotalEntryCount: 0);
        }

        var entries = fileSystemService
            .GetFoldersInPath(rootPath)
            .Select(folderPath => new FolderTopLevelEntryReadModel(
                Name: Path.GetFileName(folderPath),
                IsFolder: true
            ))
            .Concat(
                fileSystemService
                    .GetFilesInPath(rootPath, recursive: false)
                    .Select(filePath => new FolderTopLevelEntryReadModel(
                        Name: Path.GetFileName(filePath),
                        IsFolder: false
                    ))
            )
            .OrderByDescending(entry => entry.IsFolder)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FolderTopLevelEntriesReadModel(
            Entries: entries.Take(MaxTopLevelEntryCount).ToList(),
            TotalEntryCount: entries.Count
        );
    }

    public async Task ConfirmFolderAsync(string rootPath, CancellationToken cancellationToken)
    {
        EnsureIsConfirmableRootPath(rootPath);

        if (!fileSystemService.DirectoryExists(rootPath))
        {
            throw new InvalidOperationException(
                $"The folder {rootPath} does not exist and cannot be confirmed."
            );
        }

        var markerId = Guid.NewGuid();
        fileSystemService.WriteFileText(
            FolderConfirmationMarkerFile.GetFilePath(rootPath),
            markerId.ToString()
        );

        var confirmedAt = timeProvider.GetLocalNow();
        var confirmedFolder = await writeRepository.GetForUpdateAsync(rootPath, cancellationToken);

        if (confirmedFolder is null)
        {
            writeRepository.Add(
                new ConfirmedFolder
                {
                    Path = rootPath,
                    MarkerId = markerId,
                    ConfirmedAt = confirmedAt,
                }
            );
        }
        else
        {
            confirmedFolder.MarkerId = markerId;
            confirmedFolder.ConfirmedAt = confirmedAt;
        }

        await writeRepository.SaveChangesAsync(cancellationToken);
        await writeRepository.ResolveFolderNotConfirmedNotificationsAsync(
            FolderNotConfirmedNotificationMessage.GetAll(rootPath),
            confirmedAt,
            cancellationToken
        );

        logger.LogInformation("Confirmed the folder {RootPath}", rootPath);
    }

    private void EnsureIsConfirmableRootPath(string rootPath)
    {
        if (!rootProvider.GetRootPaths().Contains(rootPath, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The folder {rootPath} is not a folder that can be confirmed."
            );
        }
    }
}
