using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.LoePic.Api;

public interface ILoePicApiClient
{
    Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    );

    Task<bool> CheckImageExistsAsync(
        string apiKey,
        string imageId,
        CancellationToken cancellationToken = default
    );

    Task<AccountResponse> GetAccountAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    );
}
