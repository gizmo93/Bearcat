using Bearcat.Abstractions;

namespace Bearcat.Domain.Shared.FolderConfirmation;

public class FolderConfirmationCheck(
    ConfirmableFolderRootProvider rootProvider,
    IConfirmedFolderRepository repository,
    IFileSystemService fileSystemService
)
{
    public async Task<FolderConfirmationResult> GetFolderConfirmationAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var rootPath = rootProvider
            .GetRootPaths()
            .Where(root => FolderPathHelper.IsSameOrSubPath(childPath: path, parentPath: root))
            .MaxBy(root => root.Length);

        if (rootPath is null)
        {
            return new FolderConfirmationResult(
                FolderConfirmationState.OutsideConfirmableFolders,
                RootPath: null
            );
        }

        var markerId = await repository.GetMarkerIdAsync(rootPath, cancellationToken);

        if (markerId is null)
        {
            return new FolderConfirmationResult(FolderConfirmationState.NotConfirmed, rootPath);
        }

        return MarkerFileContainsMarkerId(rootPath, markerId.Value)
            ? new FolderConfirmationResult(FolderConfirmationState.Confirmed, rootPath)
            : new FolderConfirmationResult(FolderConfirmationState.MarkerMissing, rootPath);
    }

    private bool MarkerFileContainsMarkerId(string rootPath, Guid markerId)
    {
        var markerFileContent = fileSystemService.ReadFileTextIfExists(
            FolderConfirmationMarkerFile.GetFilePath(rootPath)
        );

        return Guid.TryParse(markerFileContent?.Trim(), out var markerFileId)
            && markerFileId == markerId;
    }
}
