using System.Text.Json;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.Extensions;
using Bearcat.ImageHosters.LoePic.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.ImageHosters.LoePic;

public class LoePic(ILoePicApiClient apiClient, ILogger<LoePic> logger)
    : IImageHoster,
        ISupportsLogin
{
    public string Name => "LoePic";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(LoePicConfig.ApiKey)];

    public async Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<LoePicConfig>();

        try
        {
            var response = await apiClient.UploadImageAsync(
                config.ApiKey,
                image,
                cancellationToken
            );

            var imageUrls = GetImageUrls(response);
            var success = imageUrls.Count > 0;

            return new UploadImageResult(
                IsSuccess: success,
                Image: image,
                ImageUrls: imageUrls,
                ErrorMessages: success ? [] : ["LoePic upload returned no image URLs."],
                ExternalId: response.Id
            );
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(
                ex,
                "Error while uploading image {ImageSource} to LoePic",
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
        return JsonSerializer.Deserialize<LoePicConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<LoePicConfig>();

        try
        {
            // Every LoePic endpoint requires the API key, so reading the account behind it
            // validates the key without leaving a leftover image on the account.
            await apiClient.GetAccountAsync(config.ApiKey, cancellationToken);

            return new TryLoginResult(IsSuccess: true);
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    private static List<ImageUrl> GetImageUrls(UploadResponse response)
    {
        // LoePic serves one WebP rendition per image, so there is no thumbnail or medium variant.
        if (string.IsNullOrWhiteSpace(response.Links?.Direct))
        {
            return [];
        }

        return [new ImageUrl(ImageSize.Full, response.Links.Direct)];
    }
}
