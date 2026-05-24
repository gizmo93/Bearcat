using Refit;

namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public interface IXFilesharingApi
{
    [Get("/account/info")]
    Task<AccountInfoResponse> GetAccountInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    Task<FileInfoResponse> GetFileInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCodes,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    Task<ApiResponse<RequestUploadResponse>> RequestUploadAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );
}
