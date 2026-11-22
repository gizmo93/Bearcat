using System.Text.Json;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.DirectUpload.Api;
using Bearcat.ImageHosters.Shared;
using Microsoft.Extensions.Logging;

namespace Bearcat.ImageHosters.DirectUpload;

public class DirectUpload(
    IDirectUploadApiClient apiClient,
    ImageUrlExistenceChecker imageUrlExistenceChecker,
    ILogger<DirectUpload> logger
) : IImageHoster, ISupportsImageOnlineCheck
{
    public string Name => "directupload.eu";

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await apiClient.UploadImageAsync(image, cancellationToken);

            var imageUrls = GetImageUrls(response);

            return new UploadImageResult(
                IsSuccess: true,
                Image: image,
                ImageUrls: imageUrls,
                ErrorMessages: [],
                DeleteUrl: response.DeleteUrl,
                ExternalId: response.ImageId
            );
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(
                ex,
                "Error while uploading image {ImageSource} to directupload.eu",
                image.Source
            );

            return new UploadImageResult(
                IsSuccess: false,
                Image: image,
                ImageUrls: [],
                ErrorMessages: [errorMessage]
            );
        }
    }

    public string SerializeConfig(IReadOnlyDictionary<string, string> config)
    {
        return JsonSerializer.Serialize(config);
    }

    public IImageHosterConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<DirectUploadConfig>(serializedConfig)!;
    }

    public Task<bool> CheckImageExistsAsync(
        IReadOnlyList<ImageUrl> imageUrls,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        return imageUrlExistenceChecker.CheckAllImageUrlsExistAsync(imageUrls, cancellationToken);
    }

    private static List<ImageUrl> GetImageUrls(UploadResponse response)
    {
        var imageUrls = new List<ImageUrl> { new(ImageSize.Full, response.DirectUrl) };

        if (!string.IsNullOrWhiteSpace(response.ThumbnailUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Thumbnail, response.ThumbnailUrl));
        }

        return imageUrls;
    }
}
