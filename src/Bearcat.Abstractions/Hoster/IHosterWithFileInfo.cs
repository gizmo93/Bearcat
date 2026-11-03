using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithFileInfo : IHoster
{
    Task<FileInfoResult> GetFileInfosAsync(
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileLinks,
        CancellationToken cancellationToken
    );

    string? ConvertToOwnFileLinkFormat(string fileLink);
}
