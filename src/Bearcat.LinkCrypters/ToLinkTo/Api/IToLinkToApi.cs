using Refit;

namespace Bearcat.LinkCrypters.ToLinkTo.Api;

public interface IToLinkToApi
{
    [Get("/api/v2/ping")]
    Task PingAsync([Authorize] string apiKey, CancellationToken cancellationToken);

    [Post("/api/v2/folders")]
    Task<CreateFolder.ResponseBody> CreateFolderAsync(
        [Body] CreateFolder.RequestBody request,
        [Authorize] string apiKey,
        CancellationToken cancellationToken
    );

    [Patch("/api/v2/folders/{folder}")]
    Task<EditFolder.ResponseBody> EditFolderAsync(
        string folder,
        [Body] EditFolder.RequestBody request,
        [Authorize] string apiKey,
        CancellationToken cancellationToken
    );
}
