using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.User;

namespace Bearcat.Hosters.Alfafile.Api;

public interface IAlfafileApiClient
{
    Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        AlfafileConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> GetUploadInfoAsync(
        AlfafileConfig config,
        string uploadId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        AlfafileConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );

    Task<InfoResponse> GetUserInfoAsync(AlfafileConfig config, CancellationToken cancellationToken);
}
