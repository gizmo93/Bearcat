using Bearcat.Hosters.Shared.XVideoSharing.Api;
using Refit;

namespace Bearcat.Hosters.Videro.Api;

public interface IVideroApi
{
    [Get("/account/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetAccountInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/upload/server")]
    Task<ApiResponse<XVideoSharingResponse>> GetUploadServerAsync(
        [Query] [AliasAs("key")] string apiKey,
        CancellationToken cancellationToken
    );

    [Get("/file/info")]
    Task<ApiResponse<XVideoSharingResponse>> GetFileInfoAsync(
        [Query] [AliasAs("key")] string apiKey,
        [Query] [AliasAs("file_code")] string fileCode,
        CancellationToken cancellationToken
    );
}
