using Bearcat.Hosters.Shared.CostAction.Api.Models;
using Refit;

namespace Bearcat.Hosters.Shared.CostAction.Api;

[Headers("Accept: application/json")]
public interface ICostActionApi
{
    [Post("/v1/file/upload_url")]
    Task<ApiResponse<UploadUrlResponse>> GetUploadUrlAsync(
        [Authorize] string apiKey,
        [Body] UploadUrlRequest request,
        CancellationToken cancellationToken
    );

    [Get("/v1/file/{id}/share")]
    Task<ApiResponse<FileShareResponse>> GetFileShareLinksAsync(
        [Authorize] string apiKey,
        string id,
        CancellationToken cancellationToken
    );

    [Post("/v1/files")]
    Task<ApiResponse<FilesInfoResponse>> GetFilesInfoAsync(
        [Authorize] string apiKey,
        [AliasAs("properties")] string properties,
        [AliasAs("per_page")] int perPage,
        [Body] FilesInfoRequest request,
        CancellationToken cancellationToken
    );

    [Post("/v1/files/move")]
    Task<ApiResponse<FilesMoveResponse>> MoveFilesAsync(
        [Authorize] string apiKey,
        [Body] FilesMoveRequest request,
        CancellationToken cancellationToken
    );

    [Post("/v1/folders/search")]
    Task<ApiResponse<FolderSearchResponse>> SearchFoldersAsync(
        [Authorize] string apiKey,
        [Body] FolderSearchRequest request,
        CancellationToken cancellationToken
    );

    [Post("/v1/folder/create")]
    Task<ApiResponse<FolderCreateResponse>> CreateFolderAsync(
        [Authorize] string apiKey,
        [Body] FolderCreateRequest request,
        CancellationToken cancellationToken
    );
}
