using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.PixelFox.Api;

public interface IPixelFoxApiClient
{
    Task<CreateSessionResponse> CreateSessionAsync(
        string apiKey,
        long fileSize,
        CancellationToken cancellationToken = default
    );

    Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    );
}
