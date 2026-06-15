using Bearcat.Abstractions.ImageHoster.Dto;
using Refit;

namespace Bearcat.ImageHosters.PixHost.Api;

public class PixHostApiClient(IPixHostApi api, HttpClient httpClient) : IPixHostApiClient
{
    public const string ApiBaseUrl = "https://api.pixhost.to";

    public async Task<UploadImageResponse> UploadImageAsync(
        ImageToUploadDto image,
        int contentType,
        CancellationToken cancellationToken = default
    )
    {
        var (stream, fileName, mediaType) = await OpenImageStreamAsync(image, cancellationToken);

        await using (stream)
        {
            var imagePart = mediaType is null
                ? new StreamPart(stream, fileName)
                : new StreamPart(stream, fileName, mediaType);

            return await api.UploadImageAsync(imagePart, contentType, cancellationToken);
        }
    }

    private async Task<(Stream Stream, string FileName, string? MediaType)> OpenImageStreamAsync(
        ImageToUploadDto image,
        CancellationToken cancellationToken
    )
    {
        switch (image.SourceType)
        {
            case ImageUploadSource.LocalFile:
            {
                var fileName = Path.GetFileName(image.Source);
                return (
                    Stream: File.OpenRead(image.Source),
                    FileName: fileName,
                    MediaType: GetMediaTypeFromFileName(fileName)
                );
            }

            case ImageUploadSource.Base64:
            {
                var bytes = Convert.FromBase64String(image.Source);
                return (Stream: new MemoryStream(bytes), FileName: "image", MediaType: null);
            }

            case ImageUploadSource.Url:
            {
                // PiXhost only accepts multipart file uploads, so the remote image has to be
                // downloaded first and then forwarded as a file.
                using var response = await httpClient.GetAsync(
                    requestUri: image.Source,
                    completionOption: HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken: cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var buffer = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var fileName = GetFileNameFromUrl(image.Source);

                var mediaType =
                    response.Content.Headers.ContentType?.MediaType
                    ?? GetMediaTypeFromFileName(fileName);

                return (Stream: new MemoryStream(buffer), FileName: fileName, MediaType: mediaType);
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(image),
                    image.SourceType,
                    "Unsupported image upload source."
                );
        }
    }

    private static string GetFileNameFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(uri.AbsolutePath);

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return "image";
    }

    private static string? GetMediaTypeFromFileName(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null,
        };
    }
}
