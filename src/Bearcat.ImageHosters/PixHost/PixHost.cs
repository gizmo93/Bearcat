using System.Text.Json;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.PixHost.Api;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.ImageHosters.PixHost;

public class PixHost(IPixHostApiClient apiClient, ILogger<PixHost> logger) : IImageHoster
{
    // PiXhost requires a content_type (0 = family safe, 1 = NSFW). Uploads are anonymous and the
    // content rating is currently hardcoded to family safe.
    private const int FamilySafeContentType = 0;

    public string Name => "PiXhost";

    public IReadOnlyList<string> ConfigurationKeys => [];

    public async Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await apiClient.UploadImageAsync(
                image: image,
                contentType: FamilySafeContentType,
                cancellationToken: cancellationToken
            );

            var imageUrls = GetImageUrls(response);
            var success = imageUrls.Count > 0;

            return new UploadImageResult(
                IsSuccess: success,
                Image: image,
                ImageUrls: imageUrls,
                ErrorMessages: success ? [] : ["PiXhost upload returned no image URLs."]
            );
        }
        catch (ApiException ex)
        {
            logger.LogError(
                ex,
                "Error while uploading image {ImageSource} to PiXhost",
                image.Source
            );

            return new UploadImageResult(
                IsSuccess: false,
                Image: image,
                ImageUrls: [],
                ErrorMessages: [GetErrorMessage(ex)]
            );
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(
                ex,
                "Error while uploading image {ImageSource} to PiXhost",
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
        return JsonSerializer.Deserialize<PixHostConfig>(serializedConfig)!;
    }

    private static List<ImageUrl> GetImageUrls(UploadImageResponse response)
    {
        var imageUrls = new List<ImageUrl>();

        if (!string.IsNullOrWhiteSpace(response.ShowUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Full, response.ShowUrl));
        }

        if (!string.IsNullOrWhiteSpace(response.ThumbnailUrl))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Thumbnail, response.ThumbnailUrl));
        }

        return imageUrls;
    }

    private static string GetErrorMessage(ApiException ex)
    {
        return (int)ex.StatusCode switch
        {
            400 => "PiXhost upload failed: bad request.",
            413 => "PiXhost upload failed: file size exceeds the 10 MB limit.",
            414 => "PiXhost upload failed: unexpected file format (allowed: gif, png, jpeg).",
            500 => "PiXhost upload failed: internal server error. Please try again later.",
            _ => $"PiXhost upload failed with status code {(int)ex.StatusCode}.",
        };
    }
}
