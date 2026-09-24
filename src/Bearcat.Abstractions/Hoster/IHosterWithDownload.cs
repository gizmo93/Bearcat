using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithDownload : IHoster
{
    bool DownloadRequiresPremium { get; }

    Task<DownloadFileResult> DownloadFileAsync(
        DownloadFileDto file,
        string targetFilePath,
        IHosterConfig hosterConfig,
        ITransferProgress progress,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Looks up the sizes of hosted files on a best effort basis.
    /// </summary>
    /// <returns>
    /// The size in bytes per file URL for every file the hoster could resolve. Files that are
    /// missing from the result have an unknown size.
    /// </returns>
    Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        IReadOnlyList<string> fileUrls,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );
}
