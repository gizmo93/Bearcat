using Refit;

namespace Bearcat.Hosters.KrakenFiles.Api;

public interface IKrakenFilesApi
{
    [Get("/api/server/available")]
    Task<AvailableServerResponse> GetAvailableServerAsync(CancellationToken cancellationToken);

    [Get("/api/file/{hash}")]
    Task<ApiResponse<FileResponse>> GetFileAsync(
        string hash,
        [Header("X-AUTH-TOKEN")] string apiToken,
        CancellationToken cancellationToken
    );

    [Get("/api/file")]
    Task<ApiResponse<ListFilesResponse>> ListFilesAsync(
        [Header("X-AUTH-TOKEN")] string apiToken,
        [Query] int page,
        [Query] int perPage,
        CancellationToken cancellationToken
    );

    [Get("/api/folder")]
    Task<FolderListResponse> ListFoldersAsync(
        [Header("X-AUTH-TOKEN")] string apiToken,
        CancellationToken cancellationToken
    );

    [Post("/api/folder")]
    Task<FolderCreateResponse> CreateFolderAsync(
        [Header("X-AUTH-TOKEN")] string apiToken,
        [Body] CreateFolderRequest request,
        CancellationToken cancellationToken
    );
}
