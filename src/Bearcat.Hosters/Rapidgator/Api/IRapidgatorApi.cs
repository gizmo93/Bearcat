using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.Folder;
using Bearcat.Hosters.Rapidgator.Api.User;
using Refit;

namespace Bearcat.Hosters.Rapidgator.Api;

public interface IRapidgatorApi
{
    [Post("/api/v2/user/login")]
    Task<ApiResponse<LoginResponse>> LoginAsync(
        [Query] string login,
        [Query] string password,
        CancellationToken cancellationToken
    );

    [Get("/api/v2/user/info")]
    Task<ApiResponse<InfoResponse>> GetUserInfoAsync(
        [Query] string token,
        CancellationToken cancellationToken
    );

    [Post("/api/v2/file/upload")]
    Task<UploadFileResponse> RequestUploadFileAsync(
        [Query] string token,
        [Query] string name,
        [Query] long size,
        [Query] string hash,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Post("/api/v2/file/change_mode")]
    Task<UploadFileResponse> ChangeFileModeAsync(
        [Query] string token,
        [Query] [AliasAs("file_id")] string fileId,
        [Query] int mode,
        CancellationToken cancellationToken
    );

    [Post("/api/v2/folder/create")]
    Task<FolderResponse> CreateFolderAsync(
        [Query] string token,
        [Query] string name,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/api/v2/folder/info")]
    Task<FolderResponse> GetFolderInfoAsync(
        [Query] string token,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/api/v2/file/upload_info?upload_id={uploadId}&token={token}")]
    Task<UploadFileResponse> GetFileStatusAsync(
        string token,
        string uploadId,
        CancellationToken cancellationToken
    );

    [Get("/api/v2/file/check_link")]
    Task<ApiResponse<CheckLinksResponse>> CheckLinkAsync(
        [Query] string token,
        [Query] [AliasAs("url")] string links,
        CancellationToken cancellationToken
    );
}
