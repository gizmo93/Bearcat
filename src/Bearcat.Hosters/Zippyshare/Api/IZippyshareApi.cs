using Refit;

namespace Bearcat.Hosters.Zippyshare.Api;

[Headers("Accept: application/json")]
public interface IZippyshareApi
{
    [Get("/api/user")]
    Task<ApiResponse<UserResponse>> GetUserAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken
    );

    [Get("/api/file/{fileId}")]
    Task<ApiResponse<FileResponse>> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken
    );
}
