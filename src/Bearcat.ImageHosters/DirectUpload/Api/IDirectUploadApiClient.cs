using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.DirectUpload.Api;

public interface IDirectUploadApiClient
{
    Task<UploadResponse> UploadImageAsync(
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    );
}
