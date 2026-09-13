using Refit;

namespace Bearcat.Hosters.CloudFam.Api;

public interface ICloudFamApi
{
    [Get("/api/v3/user/profile")]
    Task<ApiResponse<CloudFamResponse<UserProfileResponse>>> GetProfileAsync(
        [Header("X-API-Key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Post("/api/v3/upload/session")]
    Task<ApiResponse<CloudFamResponse<UploadSessionResponse>>> CreateUploadSessionAsync(
        [Header("X-API-Key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Post("/api/v3/upload/finalize")]
    Task<ApiResponse<CloudFamResponse<FinalizeUploadResponse>>> FinalizeUploadAsync(
        [Header("X-API-Key")] string apiKey,
        [Body] FinalizeUploadRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/v3/folders")]
    Task<ApiResponse<CloudFamResponse<FolderListResponse>>> ListFoldersAsync(
        [Header("X-API-Key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Post("/api/v3/folders")]
    Task<ApiResponse<CloudFamResponse<CreateFolderResponse>>> CreateFolderAsync(
        [Header("X-API-Key")] string apiKey,
        [Body] CreateFolderRequest request,
        CancellationToken cancellationToken
    );

    [Post("/api/v3/files/move")]
    Task<ApiResponse<CloudFamResponse<MoveFilesResponse>>> MoveFilesAsync(
        [Header("X-API-Key")] string apiKey,
        [Body] MoveFilesRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/v3/files")]
    Task<ApiResponse<FileListResponse>> ListFilesAsync(
        [Header("X-API-Key")] string apiKey,
        [AliasAs("page")] int page,
        [AliasAs("limit")] int limit,
        [AliasAs("q")] string? query,
        CancellationToken cancellationToken
    );

    [Get("/api/v3/file/info")]
    Task<ApiResponse<FileInfoResponse>> GetFileInfoAsync(
        [Header("X-API-Key")] string apiKey,
        [AliasAs("file_code")] string fileCodes,
        CancellationToken cancellationToken
    );
}
