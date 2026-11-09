using Bearcat.Abstractions.Hoster.Dto;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithBatchFileMove : IHosterWithFolders
{
    Task<IReadOnlyList<string>> MoveFilesToFolderAsync(
        IReadOnlyList<HosterFileToMoveDto> files,
        string folderId,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
