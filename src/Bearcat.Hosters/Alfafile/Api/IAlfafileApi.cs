using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.Folder;
using Bearcat.Hosters.Alfafile.Api.User;
using Refit;

namespace Bearcat.Hosters.Alfafile.Api;

public interface IAlfafileApi
{
    [Post("/api/v1/user/login")]
    Task<ApiResponse<LoginResponse>> LoginAsync(
        [Query] string login,
        [Query] string password,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/user/info")]
    Task<ApiResponse<InfoResponse>> GetUserInfoAsync(
        [Query] string token,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/file/upload")]
    Task<UploadFileResponse> RequestUploadFileAsync(
        [Query] string token,
        [Query] string name,
        [Query] long size,
        [Query] string hash,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Post("/api/v1/folder/create")]
    Task<FolderResponse> CreateFolderAsync(
        [Query] string token,
        [Query] string name,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/folder/info")]
    Task<FolderResponse> GetFolderInfoAsync(
        [Query] string token,
        [Query] [AliasAs("folder_id")] string? folderId,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/folder/content")]
    Task<ApiResponse<FolderContentResponse>> GetFolderContentAsync(
        [Query] string token,
        [Query] [AliasAs("folder_id")] string? folderId,
        [Query] int page,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/file/upload_info")]
    Task<UploadFileResponse> GetUploadInfoAsync(
        [Query] string token,
        [Query] [AliasAs("upload_id")] string uploadId,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/file/info")]
    Task<ApiResponse<FileInfoResponse>> GetFileInfoAsync(
        [Query] string token,
        [Query] [AliasAs("file_id")] string fileId,
        CancellationToken cancellationToken
    );

    [Get("/api/v1/file/move")]
    Task<MoveFileResponse> MoveFileAsync(
        [Query] string token,
        [Query] [AliasAs("file_id")] string fileId,
        [Query] [AliasAs("folder_id_dest")] string folderIdDest,
        CancellationToken cancellationToken
    );
}
