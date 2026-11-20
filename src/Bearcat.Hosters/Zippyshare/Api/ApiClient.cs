using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Shared;
using Refit;

namespace Bearcat.Hosters.Zippyshare.Api;

public class ApiClient(IZippyshareApi api, HttpClientProvider httpClientProvider)
    : IZippyshareApiClient
{
    public const string BaseUrl = "https://zippyshare.day";

    private const string UploadUrl = $"{BaseUrl}/api/upload";

    private const string FileNotFoundError = "File not found.";

    private const string UnauthenticatedMessage = "Unauthenticated.";

    private const string ApiKeyRejectedMessage = "Zippyshare.day rejected the API key.";

    private const int MaxParallelFileRequests = 4;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UploadFileResponse> UploadFileAsync(
        ZippyshareConfig config,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartContent = new MultipartFormDataContent();

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipartContent.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadUrl);
        request.Content = multipartContent;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var httpResponse = await httpClient.SendAsync(request, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        ThrowIfApiKeyRejected(httpResponse.StatusCode, content);

        var response = httpResponse.IsSuccessStatusCode
            ? TryDeserialize<UploadFileResponse>(content)
            : null;

        if (response is { Success: true, FileId.Length: > 0, DownloadLink.Length: > 0 })
        {
            return response;
        }

        var errorResponse = TryDeserialize<ErrorResponse>(content);

        throw new HttpRequestException(
            errorResponse?.Error
                ?? $"Zippyshare upload request failed with status code {httpResponse.StatusCode}: {errorResponse?.Message ?? content}"
        );
    }

    public async Task<IReadOnlyDictionary<string, ZippyshareFileStatus>> GetFileStatusesAsync(
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    )
    {
        var fileStatusPerFileId = new ConcurrentDictionary<string, ZippyshareFileStatus>();

        await Parallel.ForEachAsync(
            fileIds.Distinct(),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelFileRequests,
                CancellationToken = cancellationToken,
            },
            async (fileId, token) =>
            {
                fileStatusPerFileId[fileId] = await GetFileStatusAsync(fileId, token);
            }
        );

        return fileStatusPerFileId;
    }

    public async Task<UserResponse> GetUserAsync(
        ZippyshareConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetUserAsync($"Bearer {config.ApiKey}", cancellationToken);

        if (response is { StatusCode: HttpStatusCode.OK, Content.Success: true })
        {
            return response.Content;
        }

        ThrowIfApiKeyRejected(response.StatusCode, GetErrorContent(response));

        throw new HttpRequestException(
            $"Zippyshare user request failed with status code {response.StatusCode}: {GetErrorMessage(response)}",
            inner: null,
            statusCode: response.StatusCode
        );
    }

    private async Task<ZippyshareFileStatus> GetFileStatusAsync(
        string fileId,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetFileAsync(fileId, cancellationToken);

        if (response is { StatusCode: HttpStatusCode.OK, Content.Success: true })
        {
            return new ZippyshareFileStatus(
                IsOnline: true,
                FileName: response.Content.Name,
                SizeBytes: response.Content.Size,
                DownloadCount: response.Content.Downloads
            );
        }

        if (
            response.StatusCode == HttpStatusCode.NotFound
            && TryDeserialize<ErrorResponse>(GetErrorContent(response))?.Error == FileNotFoundError
        )
        {
            return new ZippyshareFileStatus(
                IsOnline: false,
                FileName: null,
                SizeBytes: null,
                DownloadCount: null
            );
        }

        throw new HttpRequestException(
            $"Zippyshare file request for {fileId} failed with status code {response.StatusCode}: {GetErrorMessage(response)}",
            inner: null,
            statusCode: response.StatusCode
        );
    }

    private static void ThrowIfApiKeyRejected(HttpStatusCode? statusCode, string? content)
    {
        if (
            statusCode == HttpStatusCode.Unauthorized
            && TryDeserialize<ErrorResponse>(content)?.Message == UnauthenticatedMessage
        )
        {
            throw new HosterCredentialsRejectedException(ApiKeyRejectedMessage);
        }
    }

    private static string? GetErrorMessage(IApiResponse response)
    {
        var content = GetErrorContent(response);
        var errorResponse = TryDeserialize<ErrorResponse>(content);

        return errorResponse?.Error ?? errorResponse?.Message ?? content;
    }

    private static string? GetErrorContent(IApiResponse response) =>
        (response.Error as ApiException)?.Content;

    private static T? TryDeserialize<T>(string? content)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
