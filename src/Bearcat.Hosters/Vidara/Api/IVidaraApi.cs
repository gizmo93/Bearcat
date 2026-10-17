using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Refit;

namespace Bearcat.Hosters.Vidara.Api;

public interface IVidaraApi
{
    [Get("/user/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetAccountInfoAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    Task<ApiResponse<XVideoSharingResponse>> GetUploadServerAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/video/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetFileInfoAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        [Query] [AliasAs("filecode")] string fileCode,
        CancellationToken cancellationToken
    );

    [Get("/folder/list")]
    Task<ApiResponse<XVideoSharingResponse>> GetFolderListAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/folder/create")]
    Task<ApiResponse<XVideoSharingResponse>> CreateFolderAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        [Query] [AliasAs("name")] string name,
        CancellationToken cancellationToken
    );

    [Get("/video/move")]
    Task<ApiResponse<XVideoSharingResponse>> MoveFileToFolderAsync(
        [Query] [AliasAs("api_key")] string apiKey,
        [Query] [AliasAs("filecode")] string fileCode,
        [Query] [AliasAs("fld_id")] string folderId,
        CancellationToken cancellationToken
    );
}
