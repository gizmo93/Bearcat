using System.Text.RegularExpressions;
using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.ImageHosters.DirectUpload.Api;

public partial class DirectUploadApiClient(HttpClient httpClient) : IDirectUploadApiClient
{
    public const string BaseUrl = "https://www.directupload.eu";

    private const string UploadEndpoint = "/api/upload_http_resize.php";
    private const string SubmitEndpoint = "/upload_a2/";

    // Don't render text into the image
    private const string ShowText = "0";

    // The upload endpoint answers with "S#<server>.<imageId>" on success and stores that exact
    // string back as the image identifier for the follow-up submit
    private const string SuccessPrefix = "S#";

    public async Task<UploadResponse> UploadImageAsync(
        ImageToUploadDto image,
        CancellationToken cancellationToken = default
    )
    {
        var prepared = await PrepareImageAsync(image, cancellationToken);

        var (uploadToken, sessionCookie) = await UploadFileAsync(prepared, cancellationToken);

        var imageId = ParseImageId(uploadToken);

        var resultPage = await SubmitUploadAsync(
            uploadToken: uploadToken,
            fileName: prepared.FileName,
            sessionCookie: sessionCookie,
            cancellationToken: cancellationToken
        );

        return ParseResultPage(resultPage, imageId);
    }

    private async Task<(string UploadToken, string? SessionCookie)> UploadFileAsync(
        PreparedImage prepared,
        CancellationToken cancellationToken
    )
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(prepared.DataUrl), "file");
        content.Add(new StringContent(prepared.FileName), "filename");
        content.Add(new StringContent(ShowText), "showtext");

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
        request.Content = content;

        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, "directupload upload", cancellationToken);

        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

        if (!body.StartsWith(SuccessPrefix, StringComparison.Ordinal))
        {
            throw new DirectUploadApiException(
                $"directupload upload was rejected with response \"{body}\"."
            );
        }

        return (UploadToken: body, SessionCookie: ExtractSessionCookie(response));
    }

    private async Task<string> SubmitUploadAsync(
        string uploadToken,
        string fileName,
        string? sessionCookie,
        CancellationToken cancellationToken
    )
    {
        using var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("img_id[]", uploadToken),
            new KeyValuePair<string, string>("file_name[]", fileName),
            new KeyValuePair<string, string>("showtext", ShowText),
        ]);

        using var request = new HttpRequestMessage(HttpMethod.Post, SubmitEndpoint);

        request.Content = content;

        if (sessionCookie is not null)
        {
            request.Headers.Add("Cookie", sessionCookie);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, "directupload submit", cancellationToken);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static UploadResponse ParseResultPage(string resultPage, string imageId)
    {
        // The result page contains every link variant in a "Linkliste[0][n] = '...'" array. We
        // read the direct image link (6), the delete link (7) and the preview shown in the BB-code
        // entry (2).
        var directUrl = MatchLinklisteEntry(resultPage, 6);

        if (directUrl is null)
        {
            throw new DirectUploadApiException(
                "directupload submit returned no direct image link."
            );
        }

        return new UploadResponse(
            ImageId: imageId,
            DirectUrl: directUrl,
            ThumbnailUrl: ExtractThumbnailUrl(resultPage),
            DeleteUrl: MatchLinklisteEntry(resultPage, 7)
        );
    }

    private static string? ExtractThumbnailUrl(string resultPage)
    {
        var bbCode = MatchLinklisteEntry(resultPage, 2);

        if (bbCode is null)
        {
            return null;
        }

        var match = BbCodeImageRegex().Match(bbCode);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ParseImageId(string uploadToken)
    {
        var separatorIndex = uploadToken.LastIndexOf('.');

        return separatorIndex >= 0 && separatorIndex < uploadToken.Length - 1
            ? uploadToken[(separatorIndex + 1)..]
            : uploadToken;
    }

    private static string? MatchLinklisteEntry(string resultPage, int index)
    {
        var match = Regex.Match(
            input: resultPage,
            pattern: $@"Linkliste\[0\]\[{index}\]\s*=\s*'([^']*)'",
            options: RegexOptions.None,
            matchTimeout: TimeSpan.FromSeconds(5)
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        return cookies
            .FirstOrDefault(cookie =>
                cookie.StartsWith("PHPSESSID=", StringComparison.OrdinalIgnoreCase)
            )
            ?.Split(';', 2)[0];
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
                var bytes = await File.ReadAllBytesAsync(image.Source, cancellationToken);
                var fileName = Path.GetFileName(image.Source);

                return BuildPreparedImage(bytes, GetMediaTypeFromFileName(fileName), fileName);
            }

            case ImageUploadSource.Base64:
            {
                var bytes = Convert.FromBase64String(image.Source);

                return BuildPreparedImage(bytes, mediaType: null, fileName: image.Name);
            }

            case ImageUploadSource.Url:
            {
                using var response = await httpClient.GetAsync(
                    image.Source,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var fileName = image.Name ?? GetFileNameFromUrl(image.Source);

                var mediaType =
                    response.Content.Headers.ContentType?.MediaType
                    ?? GetMediaTypeFromFileName(fileName);

                return BuildPreparedImage(bytes, mediaType, fileName);
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(image),
                    image.SourceType,
                    "Unsupported image upload source."
                );
        }
    }

    private static PreparedImage BuildPreparedImage(
        byte[] bytes,
        string? mediaType,
        string? fileName
    )
    {
        var resolvedMediaType = mediaType ?? "image/png";
        var resolvedFileName = EnsureFileName(fileName, resolvedMediaType);
        var dataUrl = $"data:{resolvedMediaType};base64,{Convert.ToBase64String(bytes)}";

        return new PreparedImage(dataUrl, resolvedFileName);
    }

    private static string EnsureFileName(string? fileName, string mediaType)
    {
        // directupload rejects uploads whose filename does not end in a recognised image extension
        // (e.g. a display name like "Some.Show.2023"), so we only keep an existing extension when it
        // is one of those and otherwise append the extension derived from the media type.
        if (!string.IsNullOrWhiteSpace(fileName) && GetMediaTypeFromFileName(fileName) is not null)
        {
            return fileName;
        }

        var baseName = string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;

        return baseName + GetExtensionFromMediaType(mediaType);
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
            ".bmp" => "image/bmp",
            ".heic" => "image/heic",
            _ => null,
        };
    }

    private static string GetExtensionFromMediaType(string mediaType)
    {
        return mediaType switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/heic" => ".heic",
            _ => ".png",
        };
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

        throw new DirectUploadApiException(
            $"{operation} failed with status code {(int)response.StatusCode}: {detail}"
        );
    }

    [GeneratedRegex(@"\[IMG\]([^\[]+)\[/IMG\]", RegexOptions.IgnoreCase)]
    private static partial Regex BbCodeImageRegex();

    private sealed record PreparedImage(string DataUrl, string FileName);
}
