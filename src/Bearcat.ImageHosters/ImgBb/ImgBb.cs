using System.Reflection;
using System.Text.Json;
using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.ImageHosters.Extensions;
using Bearcat.ImageHosters.ImgBb.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.ImageHosters.ImgBb;

public class ImgBb(IImgBbApiClient apiClient, ILogger<ImgBb> logger) : IImageHoster, ISupportsLogin
{
    private const string LoginTestImageResourceName =
        "Bearcat.ImageHosters.ImgBb.Resources.LoginTestImage.png.base64";

    public string Name => "ImgBB";

    public IReadOnlyList<string> ConfigurationKeys => [nameof(ImgBbConfig.ApiKey)];

    public async Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<ImgBbConfig>();

        try
        {
            var response = await apiClient.UploadImageAsync(
                config.ApiKey,
                image,
                cancellationToken
            );

            var imageUrls = GetImageUrls(response);

            var success = response.Success && imageUrls.Count > 0;

            return new UploadImageResult(
                IsSuccess: success,
                Image: image,
                ImageUrls: imageUrls,
                ErrorMessages: success ? [] : [GetErrorMessage(response)],
                DeleteUrl: response.Data?.DeleteUrl,
                ExternalId: response.Data?.Id
            );
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(ex, "Error while uploading image {ImageSource} to ImgBB", image.Source);

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
        return JsonSerializer.Deserialize<ImgBbConfig>(serializedConfig)!;
    }

    public async Task<TryLoginResult> TryLoginAsync(
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    )
    {
        var config = imageHosterConfig.As<ImgBbConfig>();

        try
        {
            await using var testImageStream = await OpenLoginTestImageStreamAsync(
                cancellationToken
            );

            var response = await apiClient.UploadImageAsync(
                apiKey: config.ApiKey,
                imageStream: testImageStream,
                fileName: "bearcat-login-test.png",
                name: "bearcat-login-test",
                expirationSeconds: 60,
                cancellationToken: cancellationToken
            );

            var imageUrls = GetImageUrls(response);
            var success = response.Success && imageUrls.Count > 0;

            return new TryLoginResult(
                IsSuccess: success,
                ErrorMessage: success ? null : GetErrorMessage(response)
            );
        }
        catch (Exception ex)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message
            );
        }
    }

    private static async Task<Stream> OpenLoginTestImageStreamAsync(
        CancellationToken cancellationToken
    )
    {
        await using var resourceStream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(LoginTestImageResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource {LoginTestImageResourceName} was not found."
            );

        using var reader = new StreamReader(resourceStream);

        var base64 = await reader.ReadToEndAsync(cancellationToken);
        return new MemoryStream(Convert.FromBase64String(base64));
    }

    private static List<ImageUrl> GetImageUrls(UploadResponse response)
    {
        var imageUrls = new List<ImageUrl>();

        if (!string.IsNullOrWhiteSpace(response.Data?.Image?.Url))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Full, response.Data.Image.Url));
        }

        if (!string.IsNullOrWhiteSpace(response.Data?.Thumbnail?.Url))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Thumbnail, response.Data.Thumbnail.Url));
        }

        if (!string.IsNullOrWhiteSpace(response.Data?.Medium?.Url))
        {
            imageUrls.Add(new ImageUrl(ImageSize.Medium, response.Data.Medium.Url));
        }

        return imageUrls;
    }

    private static string GetErrorMessage(UploadResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Error?.Message))
        {
            return response.Error.Message;
        }

        return response.Status == 0
            ? "ImgBB upload failed."
            : $"ImgBB upload failed with status {response.Status}.";
    }
}
