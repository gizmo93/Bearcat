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

    [Get("/folder/list")]
    Task<FolderListResponse> GetFolderListAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("fld_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/folder/create")]
    Task<FolderCreateResponse> CreateFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] string name,
        CancellationToken cancellationToken
    );

    [Get("/file/set_folder")]
    Task<StatusResponse> SetFileFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        [Query] [AliasAs("fld_id")] string folderId,
        CancellationToken cancellationToken
    );

    [Get("/file/set_property")]
    Task<StatusResponse> SetFilePropertiesAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        [Query] [AliasAs("premium_only")] int? premiumOnly,
        CancellationToken cancellationToken
    );
}
