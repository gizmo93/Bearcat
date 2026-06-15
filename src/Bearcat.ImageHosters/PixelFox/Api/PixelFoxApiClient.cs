using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.PixelFox.Api;

public class PixelFoxApiClient(HttpClient httpClient) : IPixelFoxApiClient
{
    public const string BaseUrl = "https://pixelfox.cc";

    // Variant identifiers used both when requesting derivatives and when reading them back from the
    // upload response.
    public const string OriginalFamily = "original";
    public const string MediumSize = "medium";
    public const string SmallSize = "small";

    private const string SessionsEndpoint = "/api/v1/upload/sessions";

    // Release screenshots are family safe, so uploads are never flagged as sensitive content.
    private const bool IsNsfw = false;

    // The default profile relies on per-account settings, so we explicitly request the original
    // image plus a medium and a small derivative to guarantee thumbnails are generated.
    private static readonly ProcessingRequest StandardProcessing = new(
        Profile: "custom",
        Derivatives:
        [
            new DerivativeRequest(OriginalFamily, MediumSize),
            new DerivativeRequest(OriginalFamily, SmallSize),
        ]
    );

    public async Task<CreateSessionResponse> CreateSessionAsync(
        string apiKey,
        long fileSize,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SessionsEndpoint)
        {
            Content = JsonContent.Create(
                new CreateSessionRequest(fileSize, IsNsfw, StandardProcessing)
            ),
        };
        request.Headers.Add("X-API-Key", apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, "PixelFox session creation", cancellationToken);

        return await ReadJsonAsync<CreateSessionResponse>(response, cancellationToken);
    }

    public async Task<UploadResponse> UploadImageAsync(
        string apiKey,
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    )
    {
        var prepared = await PrepareImageAsync(image, cancellationToken);

        await using (prepared.Content)
        {
            var session = await CreateSessionAsync(apiKey, prepared.SizeInBytes, cancellationToken);

            if (
                string.IsNullOrWhiteSpace(session.UploadUrl)
                || string.IsNullOrWhiteSpace(session.Token)
            )
            {
                throw new PixelFoxApiException(
                    "PixelFox session creation returned no upload URL or token."
                );
            }

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(prepared.Content);

            if (prepared.MediaType is not null)
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(prepared.MediaType);
            }

            content.Add(fileContent, "file", prepared.FileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, session.UploadUrl);
            request.Content = content;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            await EnsureSuccessAsync(response, "PixelFox upload", cancellationToken);

            return await ReadJsonAsync<UploadResponse>(response, cancellationToken);
        }
    }

    private async Task<PreparedImage> PrepareImageAsync(
        ImageToUploadDto image,
        CancellationToken cancellationToken
    )
    {
        switch (image.SourceType)
        {
            case ImageUploadSource.LocalFile:
            {
                var fileName = Path.GetFileName(image.Source);
                var fileInfo = new FileInfo(image.Source);

                return new PreparedImage(
                    Content: File.OpenRead(image.Source),
                    SizeInBytes: fileInfo.Length,
                    FileName: fileName,
                    MediaType: GetMediaTypeFromFileName(fileName)
                );
            }

            case ImageUploadSource.Base64:
            {
                var bytes = Convert.FromBase64String(image.Source);
                return new PreparedImage(new MemoryStream(bytes), bytes.Length, "image", null);
            }

            case ImageUploadSource.Url:
            {
                // PixelFox only accepts multipart file uploads, so a remote image has to be
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

                return new PreparedImage(
                    new MemoryStream(buffer),
                    buffer.Length,
                    fileName,
                    mediaType
                );
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(image),
                    image.SourceType,
                    "Unsupported image upload source."
                );
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken
    )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body.Trim();

        throw new PixelFoxApiException(
            $"{operation} failed with status code {(int)response.StatusCode}: {detail}"
        );
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

        return result ?? throw new PixelFoxApiException("PixelFox returned an empty response.");
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
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            _ => null,
        };
    }

    private sealed record PreparedImage(
        Stream Content,
        long SizeInBytes,
        string FileName,
        string? MediaType
    );
}
