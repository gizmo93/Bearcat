using Refit;

namespace Bearcat.ImageHosters.LoePic.Api;

public interface ILoePicApi
{
    [Multipart]
    [Post("/api/upload")]
    Task<ApiResponse<LoePicResponse<UploadResponse>>> UploadFileAsync(
        [Header("X-API-Key")] string apiKey,
        [AliasAs("file")] StreamPart file,
        CancellationToken cancellationToken
    );

    [Post("/api/upload")]
    Task<ApiResponse<LoePicResponse<UploadResponse>>> UploadBase64Async(
        [Header("X-API-Key")] string apiKey,
        [Body] Base64UploadRequest request,
        CancellationToken cancellationToken
    );

    [Post("/api/upload")]
    Task<ApiResponse<LoePicResponse<UploadResponse>>> UploadUrlAsync(
        [Header("X-API-Key")] string apiKey,
        [Body(BodySerializationMethod.UrlEncoded)] UrlUploadRequest request,
        CancellationToken cancellationToken
    );

    [Get("/api/me")]
    Task<ApiResponse<LoePicResponse<AccountResponse>>> GetAccountAsync(
        [Header("X-API-Key")] string apiKey,
        CancellationToken cancellationToken
    );
}
