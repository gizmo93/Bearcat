using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Refit;

namespace Bearcat.Hosters.DoodStream.Api;

public interface IDoodStreamApi
{
    [Get("/account/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetAccountInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    Task<ApiResponse<XVideoSharingResponse>> GetUploadServerAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetFileInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCodes,
        CancellationToken cancellationToken
    );

    [Get("/folder/list")]
    Task<ApiResponse<XVideoSharingResponse>> GetFolderListAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("fld_id")] string parentFolderId,
        CancellationToken cancellationToken
    );

    [Get("/folder/create")]
    Task<ApiResponse<XVideoSharingResponse>> CreateFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("name")] string name,
        CancellationToken cancellationToken
    );

    [Get("/file/move")]
    Task<ApiResponse<XVideoSharingResponse>> MoveFileToFolderAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        [Query] [AliasAs("fld_id")] string folderId,
        CancellationToken cancellationToken
    );
}
