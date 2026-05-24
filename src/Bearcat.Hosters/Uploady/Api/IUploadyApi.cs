using Bearcat.Hosters.Shared.XFilesharing.Api;
using Refit;

namespace Bearcat.Hosters.Uploady.Api;

public interface IUploadyApi : IXFilesharingApi
{
    [Get("/account/info")]
    new Task<AccountInfoResponse> GetAccountInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    new Task<FileInfoResponse> GetFileInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCodes,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    new Task<ApiResponse<RequestUploadResponse>> RequestUploadAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );
}
