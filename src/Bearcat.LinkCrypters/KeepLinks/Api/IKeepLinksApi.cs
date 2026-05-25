using Bearcat.LinkCrypters.KeepLinks.Api.ProtectLinks;
using Refit;

namespace Bearcat.LinkCrypters.KeepLinks.Api;

public interface IKeepLinksApi
{
    [Get("/api.php?list=1&page=1&pagesize=1&output=json")]
    Task<string> GetLinksAsync(
        [Query] [AliasAs("apihash")] string apiKey,
        CancellationToken cancellationToken = default
    );

    [Post("/api.php")]
    Task<Response> ProtectLinkAsync(
        [Body] MultipartFormDataContent request,
        CancellationToken cancellationToken = default
    );

    [Post("/api.php")]
    Task<Response> UpdateContainerAsync(
        [Body] MultipartFormDataContent request,
        CancellationToken cancellationToken = default
    );
}
