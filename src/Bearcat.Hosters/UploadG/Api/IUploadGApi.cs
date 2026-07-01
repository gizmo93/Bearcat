using Refit;

namespace Bearcat.Hosters.UploadG.Api;

public interface IUploadGApi
{
    [Get("/user/space-usage")]
    Task<ApiResponse<SpaceUsageResponse>> GetSpaceUsageAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken
    );

    [Post("/s3/multipart/create")]
    Task<MultipartCreateResponse> CreateMultipartUploadAsync(
        [Header("Authorization")] string authorization,
        [Body] MultipartCreateRequest request,
        CancellationToken cancellationToken
    );

    [Post("/s3/multipart/batch-sign-part-urls")]
    Task<BatchSignPartUrlsResponse> SignPartUrlsAsync(
        [Header("Authorization")] string authorization,
        [Body] BatchSignPartUrlsRequest request,
        CancellationToken cancellationToken
    );

    [Post("/s3/multipart/complete")]
    Task<StatusResponse> CompleteMultipartUploadAsync(
        [Header("Authorization")] string authorization,
        [Body] MultipartCompleteRequest request,
        CancellationToken cancellationToken
    );

    [Post("/s3/entries")]
    Task<UploadFileResponse> CreateS3EntryAsync(
        [Header("Authorization")] string authorization,
        [Body] CreateS3EntryRequest request,
        CancellationToken cancellationToken
    );

    [Get("/drive/file-entries")]
    Task<FileEntryListResponse> ListFileEntriesAsync(
        [Header("Authorization")] string authorization,
        [Query] int perPage,
        [Query] string? type,
        [Query] string? query,
        [Query] string? parentIds,
        CancellationToken cancellationToken
    );

    [Post("/folders")]
    Task<CreateFolderResponse> CreateFolderAsync(
        [Header("Authorization")] string authorization,
        [Body] CreateFolderRequest request,
        CancellationToken cancellationToken
    );

    [Get("/file-entries/{entryId}/shareable-link")]
    Task<ApiResponse<ShareableLinkResponse>> GetShareableLinkAsync(
        [Header("Authorization")] string authorization,
        long entryId,
        CancellationToken cancellationToken
    );

    [Post("/file-entries/{entryId}/shareable-link")]
    Task<ShareableLinkResponse> CreateShareableLinkAsync(
        [Header("Authorization")] string authorization,
        long entryId,
        [Body] CreateShareableLinkRequest request,
        CancellationToken cancellationToken
    );

    [Post("/file-entries/move")]
    Task<StatusResponse> MoveEntriesAsync(
        [Header("Authorization")] string authorization,
        [Body] MoveEntriesRequest request,
        CancellationToken cancellationToken
    );
}
