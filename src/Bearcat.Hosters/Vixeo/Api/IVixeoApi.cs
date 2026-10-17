using Refit;

namespace Bearcat.Hosters.Vixeo.Api;

public interface IVixeoApi
{
    [Get("/account")]
    Task<ApiResponse<VixeoResponse>> GetAccountAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/getUploadSrv")]
    Task<ApiResponse<UploadServerResponse>> GetUploadServerAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/check-status")]
    Task<ApiResponse<VideoStatusResponse>> GetVideoStatusAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("code")] string videoId,
        CancellationToken cancellationToken
    );
}
