using Refit;

namespace Bearcat.Hosters.Fast2Share.Api;

public interface IFast2ShareApi
{
    [Get("/v1/user")]
    Task<ApiResponse<UserResponse>> GetUserAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken
    );

    [Get("/v1/folders")]
    Task<ApiResponse<FolderListResponse>> ListFoldersAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken
    );

    [Post("/v1/folders")]
    Task<ApiResponse<FolderResponse>> CreateFolderAsync(
        [Header("Authorization")] string authorization,
        [Body] CreateFolderRequest request,
        CancellationToken cancellationToken
    );

    [Post("/v1/uploads")]
    Task<ApiResponse<CreateUploadResponse>> CreateUploadAsync(
        [Header("Authorization")] string authorization,
        [Body] CreateUploadRequest request,
        CancellationToken cancellationToken
    );

    [Post("/v1/uploads/{uuid}/confirm")]
    Task<ApiResponse<CreateUploadResponse>> ConfirmUploadAsync(
        [Header("Authorization")] string authorization,
        string uuid,
        [Body] ConfirmUploadRequest request,
        CancellationToken cancellationToken
    );

    [Get("/v1/files/{uuid}")]
    Task<ApiResponse<FileResponse>> GetFileAsync(
        [Header("Authorization")] string authorization,
        string uuid,
        CancellationToken cancellationToken
    );

    [Get("/v1/files/{uuid}/status")]
    Task<ApiResponse<FileStatusResponse>> GetFileStatusAsync(
        [Header("Authorization")] string authorization,
        string uuid,
        CancellationToken cancellationToken
    );

    [Get("/v1/files/{uuid}/download")]
    Task<ApiResponse<FileDownloadResponse>> GetFileDownloadAsync(
        [Header("Authorization")] string authorization,
        string uuid,
        CancellationToken cancellationToken
    );

    [Post("/v1/files/{uuid}/move")]
    Task<ApiResponse<MoveFileResponse>> MoveFileAsync(
        [Header("Authorization")] string authorization,
        string uuid,
        [Body] MoveFileRequest request,
        CancellationToken cancellationToken
    );
}
