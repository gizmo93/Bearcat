namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithFolders : IHoster
{
    Task<string> CreateFolderAsync(
        string folderName,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        string fileUrl,
        string? externalId,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
