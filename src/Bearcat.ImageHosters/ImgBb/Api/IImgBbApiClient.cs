using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.ImgBb.Api;

public interface IImgBbApiClient
{
    Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    );

    Task<UploadResponse> UploadImageAsync(
        string apiKey,
        Stream imageStream,
        string fileName,
        string? name,
        int? expirationSeconds,
        CancellationToken cancellationToken = default
    );
}
