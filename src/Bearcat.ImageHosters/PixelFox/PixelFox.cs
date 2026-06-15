using System.Text.Json;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.Extensions;
using Bearcat.ImageHosters.PixelFox.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.ImageHosters.PixelFox;

public class PixelFox(IPixelFoxApiClient apiClient, ILogger<PixelFox> logger)
    : IImageHoster,
        ISupportsLogin
{
    // The PixelFox API requires an API key for every request. Creating an upload session validates
    // the key without leaving a leftover image on the account, so it doubles as the login check.
    private const long LoginProbeFileSize = 1;

    public string Name => "PixelFox";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(PixelFoxConfig.ApiKey)];

    public async Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<PixelFoxConfig>();

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
                ErrorMessages: success ? [] : ["PixelFox upload returned no image URLs."],
                ExternalId: response.ImageUuid
            );
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(
                ex,
                "Error while uploading image {ImageSource} to PixelFox",
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
        return JsonSerializer.Deserialize<PixelFoxConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<PixelFoxConfig>();

        try
        {
            await apiClient.CreateSessionAsync(
                config.ApiKey,
                LoginProbeFileSize,
                cancellationToken
            );

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
        var imageUrls = new List<ImageUrl>();

        // The original is always retained and immediately addressable.
        var fullUrl = response.Url ?? response.StableUrl;

        if (!string.IsNullOrWhiteSpace(fullUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Full, fullUrl));
        }

        // Derivatives are generated asynchronously, but stable_variants exposes predictable URLs
        // that are safe to hold on to even before processing finishes - they resolve once the file
        // exists. So we emit them regardless of the ready flag.
        var mediumUrl = GetStableVariantUrl(
            response,
            PixelFoxApiClient.OriginalFamily,
            PixelFoxApiClient.MediumSize
        );

        if (!string.IsNullOrWhiteSpace(mediumUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Medium, mediumUrl));
        }

        var thumbnailUrl = GetStableVariantUrl(
            response,
            PixelFoxApiClient.OriginalFamily,
            PixelFoxApiClient.SmallSize
        );

        if (!string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Thumbnail, thumbnailUrl));
        }

        return imageUrls;
    }

    private static string? GetStableVariantUrl(UploadResponse response, string family, string size)
    {
        if (response.StableVariants is null)
        {
            return null;
        }

        if (!response.StableVariants.TryGetValue(family, out var sizes))
        {
            return null;
        }

        return sizes.TryGetValue(size, out var variant) ? variant.Url : null;
    }
}
