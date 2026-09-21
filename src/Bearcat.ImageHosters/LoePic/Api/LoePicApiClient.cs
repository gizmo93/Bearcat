using System.Text.Json;
using Bearcat.Abstractions.ImageHoster.Dto;
using Refit;

namespace Bearcat.ImageHosters.LoePic.Api;

public class LoePicApiClient(ILoePicApi api) : ILoePicApiClient
{
    public const string BaseUrl = "https://www.loepic.me";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    )
    {
        switch (image.SourceType)
        {
            case ImageUploadSource.LocalFile:
            {
                var fileName = Path.GetFileName(image.Source);
                var mediaType = GetMediaTypeFromFileName(fileName);

                await using var stream = File.OpenRead(image.Source);

                var filePart = mediaType is null
                    ? new StreamPart(stream, fileName)
                    : new StreamPart(stream, fileName, mediaType);

                return ReadContent(
                    await api.UploadFileAsync(apiKey, filePart, cancellationToken),
                    "LoePic upload"
                );
            }

            case ImageUploadSource.Base64:
            {
                var name = string.IsNullOrWhiteSpace(image.Name) ? null : image.Name;

                return ReadContent(
                    await api.UploadBase64Async(
                        apiKey,
                        new Base64UploadRequest(image.Source, name),
                        cancellationToken
                    ),
                    "LoePic upload"
                );
            }

            case ImageUploadSource.Url:
                return ReadContent(
                    await api.UploadUrlAsync(
                        apiKey,
                        new UrlUploadRequest(image.Source),
                        cancellationToken
                    ),
                    "LoePic upload"
                );

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(image),
                    image.SourceType,
                    "Unsupported image upload source."
                );
        }
    }

    public async Task<AccountResponse> GetAccountAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        return ReadContent(
            await api.GetAccountAsync(apiKey, cancellationToken),
            "LoePic account lookup"
        );
    }

    private static T ReadContent<T>(ApiResponse<LoePicResponse<T>> response, string operationName)
    {
        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new LoePicApiException(
                $"{operationName} failed with status code {(int?)response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        if (!response.Content.Success || response.Content.Data is null)
        {
            throw new LoePicApiException(
                $"{operationName} failed with status code {(int?)response.StatusCode}: "
                    + (response.Content.Error?.Message ?? "no data was returned")
            );
        }

        return response.Content.Data;
    }

    private static string? GetErrorMessage(IApiResponse response)
    {
        var content = (response.Error as ApiException)?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var error = JsonSerializer.Deserialize<LoePicResponse<object>>(
                content,
                JsonSerializerOptions
            );

            return error?.Error?.Message ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static string? GetMediaTypeFromFileName(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            _ => null,
        };
    }
}
