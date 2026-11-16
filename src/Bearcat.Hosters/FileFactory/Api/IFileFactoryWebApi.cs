using Bearcat.Hosters.FileFactory.Api.Requests;
using Bearcat.Hosters.FileFactory.Api.Responses;
using Refit;

namespace Bearcat.Hosters.FileFactory.Api;

public interface IFileFactoryWebApi
{
    [Post("/api/auth")]
    [Headers("Origin: " + ApiClient.WebApiBaseUrl)]
    Task<ApiResponse<SignInResponse>> SignInAsync(
        [Body] SignInRequest request,
        CancellationToken cancellationToken
    );

    [Post("/api/upload/r2/sign")]
    Task<ApiResponse<SignUploadResponse>> SignUploadAsync(
        [Header("Cookie")] string cookie,
        [Body] SignUploadRequest request,
        CancellationToken cancellationToken
    );

    [Post("/api/upload/r2/complete")]
    Task<ApiResponse<CompleteUploadResponse>> CompleteUploadAsync(
        [Header("Cookie")] string cookie,
        [Body] CompleteUploadRequest request,
        CancellationToken cancellationToken
    );

    [Post("/api/tools/link-checker")]
    Task<ApiResponse<LinkCheckResponse>> CheckLinksAsync(
        [Header("Cookie")] string cookie,
        [Body] LinkCheckRequest request,
        CancellationToken cancellationToken
    );
}
