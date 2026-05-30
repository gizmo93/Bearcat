using Bearcat.Hosters.Shared.XFilesharing.Api;
using Refit;

namespace Bearcat.Hosters.DDownload.Api;

public interface IDDownloadApi : IXFilesharingApi
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

    [Get("/folder/list")]
    new Task<FolderListResponse> GetFolderListAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("fld_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/folder/create")]
    new Task<FolderCreateResponse> CreateFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] string name,
        CancellationToken cancellationToken
    );

    [Get("/file/set_folder")]
    new Task<StatusResponse> SetFileFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        [Query] [AliasAs("fld_id")] string folderId,
        CancellationToken cancellationToken
    );
}
