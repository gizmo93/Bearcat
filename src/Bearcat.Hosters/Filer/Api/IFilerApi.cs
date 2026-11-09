using Bearcat.Hosters.Filer.Api.Requests;
using Bearcat.Hosters.Filer.Api.Responses;
using Refit;

namespace Bearcat.Hosters.Filer.Api;

public interface IFilerApi
{
    [Get("/api/app/csrf")]
    Task<ApiResponse<CsrfTokenResponse>> GetCsrfTokenAsync(CancellationToken cancellationToken);

    [Post("/api/user/login")]
    [Headers("Origin: " + ApiClient.ApiBaseUrl)]
    Task<ApiResponse<MessageResponse>> LoginAsync(
        [Header("X-CSRF-Token")] string csrfToken,
        [Header("Cookie")] string cookie,
        [Body] LoginRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/upload/server")]
    Task<ApiResponse<UploadServerResponse>> GetUploadServerAsync(
        [Header("Cookie")] string cookie,
        CancellationToken cancellationToken
    );

    [Post("/api/user/folders")]
    [Headers("Origin: " + ApiClient.ApiBaseUrl)]
    Task<ApiResponse<CreateFolderResponse>> CreateFolderAsync(
        [Header("Cookie")] string cookie,
        [Body] CreateFolderRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/user/folders/search")]
    Task<ApiResponse<FolderSearchResponse>> SearchFoldersAsync(
        [Header("Cookie")] string cookie,
        [AliasAs("q")] string query,
        CancellationToken cancellationToken
    );

    [Post("/api/user/batch/move")]
    [Headers("Origin: " + ApiClient.ApiBaseUrl)]
    Task<ApiResponse<MoveFilesResponse>> MoveFilesAsync(
        [Header("Cookie")] string cookie,
        [Body] MoveFilesRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/profile.json")]
    Task<ApiResponse<ProfileResponse>> GetProfileAsync(
        [Authorize("Basic")] string basicCredentials,
        CancellationToken cancellationToken
    );

    [Get("/api/dl/{fileHash}.json")]
    Task<HttpResponseMessage> GetDownloadLinkAsync(
        [Authorize("Basic")] string basicCredentials,
        string fileHash,
        CancellationToken cancellationToken
    );

    [Get("/api/multi_status/{fileHashes}.json")]
    Task<ApiResponse<MultiStatusResponse>> GetMultiStatusAsync(
        string fileHashes,
        CancellationToken cancellationToken
    );
}
