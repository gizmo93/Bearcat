using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.PixHost.Api;

public interface IPixHostApiClient
{
    Task<UploadImageResponse> UploadImageAsync(
        ImageToUploadDto image,
        int contentType,
        CancellationToken cancellationToken = default
    );
}
