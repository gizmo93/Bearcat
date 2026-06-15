using Refit;

namespace Bearcat.ImageHosters.PixHost.Api;

public interface IPixHostApi
{
    [Multipart]
    [Headers("Accept: application/json")]
    [Post("/images")]
    Task<UploadImageResponse> UploadImageAsync(
        [AliasAs("img")] StreamPart image,
        [AliasAs("content_type")] int contentType,
        CancellationToken cancellationToken
    );
}
