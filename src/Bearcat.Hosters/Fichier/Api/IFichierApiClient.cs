using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Fichier.Api.Upload;
using Bearcat.Hosters.Fichier.Api.User;

namespace Bearcat.Hosters.Fichier.Api;

public interface IFichierApiClient
{
    Task<EndUploadResponse> UploadFileAsync(
        FichierConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        FichierConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        FichierConfig config,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        FichierConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        FichierConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );

    Task<UserInfoResponse> GetUserInfoAsync(
        FichierConfig config,
        CancellationToken cancellationToken
    );

    Task DownloadFileAsync(
        FichierConfig config,
        string fileUrl,
        string targetFilePath,
        ITransferProgress progress,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    );
}
