using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Shared;
using Refit;

namespace Bearcat.Hosters.Vixeo.Api;

public class ApiClient(IVixeoApi api, HttpClientProvider httpClientProvider)
{
    public const string ApiBaseUrl = "https://vidsonic.net/api/v1";

    private const string MimeType = "application/octet-stream";

    private const string OnlineStatus = "online";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadedVideoResult> UploadFileAsync(
        VixeoConfig config,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        var uploadServer = ReadContent(
            await api.GetUploadServerAsync(config.ApiKey, cancellationToken),
            "upload server"
        );
        var uploadUrl =
            uploadServer.Server
            ?? throw new HttpRequestException(
                $"Vixeo upload server request failed: {uploadServer.Message}"
            );

        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        multipartContent.Add(new StringContent(config.ApiKey), "apiKey");

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        multipartContent.Add(fileContent, "video", fileName);

        using var httpResponse = await httpClient.PostAsync(
            uploadUrl,
            multipartContent,
            cancellationToken
        );

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException(
                $"Vixeo rejected the API key: {GetErrorMessage(content)}"
            );
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Vixeo upload request failed with status code {httpResponse.StatusCode}: {GetErrorMessage(content)}",
                inner: null,
                statusCode: httpResponse.StatusCode
            );
        }

        var uploadedVideo = JsonSerializer.Deserialize<UploadVideoResponse>(
            content,
            JsonSerializerOptions
        );

        return uploadedVideo?.Data
            ?? throw new HttpRequestException(
                $"Vixeo upload response contained no video: {content}"
            );
    }

    public async Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        VixeoConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        var fileUrlsPerVideoId = files
            .DistinctBy(file => file.Url)
            .Select(file => new { VideoId = file.ExternalId ?? ExtractVideoId(file.Url), file.Url })
            .Where(entry => entry.VideoId is not null)
            .GroupBy(entry => entry.VideoId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.Ordinal
            );

        var isOnlinePerFileUrl = new Dictionary<string, bool>();

        foreach (var (videoId, fileUrls) in fileUrlsPerVideoId)
        {
            var isOnline = await IsVideoOnlineAsync(config, videoId, cancellationToken);

            foreach (var fileUrl in fileUrls)
            {
                isOnlinePerFileUrl[fileUrl] = isOnline;
            }
        }

        return isOnlinePerFileUrl;
    }

    public async Task EnsureCredentialsAcceptedAsync(
        VixeoConfig config,
        CancellationToken cancellationToken
    )
    {
        var account = ReadContent(
            await api.GetAccountAsync(config.ApiKey, cancellationToken),
            "account"
        );

        if (!account.Success)
        {
            throw new HttpRequestException($"Vixeo account request failed: {account.Message}");
        }
    }

    public static string? ExtractVideoId(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pathSegments is ["e" or "d", var videoId, ..] ? videoId : null;
    }

    private async Task<bool> IsVideoOnlineAsync(
        VixeoConfig config,
        string videoId,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetVideoStatusAsync(config.ApiKey, videoId, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        var videoStatus = ReadContent(response, "status check");

        if (videoStatus.Status is null)
        {
            throw new HttpRequestException(
                $"Vixeo status check for video {videoId} failed: {videoStatus.Message}"
            );
        }

        return string.Equals(videoStatus.Status, OnlineStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static T ReadContent<T>(ApiResponse<T> response, string operationName)
    {
        if (response.IsSuccessStatusCode && response.Content is not null)
        {
            return response.Content;
        }

        var errorMessage = GetErrorMessage((response.Error as ApiException)?.Content);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HosterCredentialsRejectedException(
                $"Vixeo rejected the API key: {errorMessage}"
            );
        }

        throw new HttpRequestException(
            $"Vixeo {operationName} request failed with status code {response.StatusCode}: {errorMessage}",
            inner: null,
            statusCode: response.StatusCode
        );
    }

    private static string? GetErrorMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            return JsonSerializer
                    .Deserialize<VixeoResponse>(content, JsonSerializerOptions)
                    ?.Message
                ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}
