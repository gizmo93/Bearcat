using Bearcat.Hosters.Rapidgator.Api.File;

namespace Bearcat.Hosters.Rapidgator.Api;

public interface IRapidgatorApiClient
{
    Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> GetUploadInfoAsync(
        RapidgatorConfig config,
        string uploadId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        RapidgatorConfig config,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken
    );
}
