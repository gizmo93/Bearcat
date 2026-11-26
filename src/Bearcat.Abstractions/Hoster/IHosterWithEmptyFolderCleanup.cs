using Bearcat.Abstractions.Hoster.Dto;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithEmptyFolderCleanup : IHosterWithFolders
{
    Task<IReadOnlyList<HosterFolderContentStateDto>> GetFolderContentStatesAsync(
        IReadOnlyList<string> folderIds,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );

    Task DeleteFolderAsync(
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
