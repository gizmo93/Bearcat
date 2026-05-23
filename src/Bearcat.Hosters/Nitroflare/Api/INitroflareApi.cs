using Bearcat.Hosters.Nitroflare.Api.File;
using Refit;

namespace Bearcat.Hosters.Nitroflare.Api;

public interface INitroflareApi
{
    [Get("/plugins/fileupload/getServer")]
    Task<string> GetUploadServerAsync(CancellationToken cancellationToken);

    [Get("/api/v2/getFileInfo")]
    Task<ApiResponse<FileInfoResponse>> GetFileInfoAsync(
        [Query] string files,
        CancellationToken cancellationToken
    );
}
