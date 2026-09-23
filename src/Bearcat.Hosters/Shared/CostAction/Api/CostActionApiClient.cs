using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Hosters.Shared.CostAction.Api.Models;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Shared.CostAction.Api;

public class CostActionApiClient(
    ICostActionApi api,
    HttpClientProvider httpClientProvider,
    ILogger<CostActionApiClient> logger
) : ICostActionApiClient
{
    public const string ApiBaseUrl = "https://api.costaction.com";

    private const string SuccessStatus = "success";

    private const string DownloadCountProperty = "download_count";

    private const int MaxFileIdsPerRequest = 100;

    private const int RateLimitAttempts = 5;

    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public TimeSpan MaximumRateLimitDelay { get; set; } = TimeSpan.FromSeconds(60);

    public async Task<CostActionUploadResult> UploadFileAsync(
        string apiKey,
        string appType,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var uploadUrl = await SendAsync(
            () => api.GetUploadUrlAsync(apiKey, new UploadUrlRequest(appType), cancellationToken),
            "Upload URL request",
            cancellationToken
        );

        if (string.IsNullOrWhiteSpace(uploadUrl.Url))
        {
            throw new HttpRequestException("Upload URL response contained no URL");
        }

        var fileId = await SendToStorageAsync(
            uploadUrl.Url,
            stream,
            fileName,
            fileSize,
            cancellationToken
        );

        var share = await SendAsync(
            () => api.GetFileShareLinksAsync(apiKey, fileId, cancellationToken),
            "File share link request",
            cancellationToken
        );

        var fileUrl = share.Links?.ForDownloading?.Standard;

        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new HttpRequestException(
                $"File share link response for {fileId} contained no link"
            );
        }

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            await MoveFileToFolderAsync(apiKey, fileId, folderId, cancellationToken);
        }

        return new CostActionUploadResult(FileId: fileId, FileUrl: fileUrl);
    }

    public async Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckFilesAsync(
        string apiKey,
        string appType,
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    )
    {
        var statusPerFileId = new Dictionary<string, LinkCheckStatus>(StringComparer.Ordinal);

        foreach (var chunk in fileIds.Distinct(StringComparer.Ordinal).Chunk(MaxFileIdsPerRequest))
        {
            var response = await SendAsync(
                () =>
                    api.GetFilesInfoAsync(
                        apiKey,
                        DownloadCountProperty,
                        MaxFileIdsPerRequest,
                        new FilesInfoRequest(appType, chunk),
                        cancellationToken
                    ),
                "Files info request",
                cancellationToken
            );

            var filesById = (response.Files ?? [])
                .Where(file => file.Id is not null)
                .ToDictionary(file => file.Id!, StringComparer.Ordinal);

            foreach (var fileId in chunk)
            {
                statusPerFileId[fileId] =
                    filesById.TryGetValue(fileId, out var file) && !file.IsDeleted
                        ? new LinkCheckStatus(IsOnline: true, DownloadCount: file.DownloadCount)
                        : new LinkCheckStatus(IsOnline: false, DownloadCount: null);
            }
        }

        return statusPerFileId;
    }

    public async Task<string> CreateFolderAsync(
        string apiKey,
        string appType,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var existingFolderId = await FindRootFolderIdAsync(
            apiKey,
            appType,
            folderName,
            cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId.Value.ToString();
        }

        logger.LogInformation(
            "Creating CostAction folder {FolderName} for {AppType}",
            folderName,
            appType
        );

        var response = await SendRawAsync(
            () =>
                api.CreateFolderAsync(
                    apiKey,
                    new FolderCreateRequest(
                        Name: folderName,
                        CanBeCopied: false,
                        AppType: appType,
                        ParentFolderId: null
                    ),
                    cancellationToken
                ),
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var concurrentlyCreatedFolderId = await FindRootFolderIdAsync(
                apiKey,
                appType,
                folderName,
                cancellationToken
            );

            if (concurrentlyCreatedFolderId is not null)
            {
                return concurrentlyCreatedFolderId.Value.ToString();
            }
        }

        var created = ReadContent(response, "Folder creation");

        return created.Folder?.Id.ToString()
            ?? throw new HttpRequestException(
                $"Folder creation for {folderName} returned no folder"
            );
    }

    public async Task MoveFileToFolderAsync(
        string apiKey,
        string fileId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var response = await SendAsync(
            () =>
                api.MoveFilesAsync(
                    apiKey,
                    new FilesMoveRequest([fileId], folderId),
                    cancellationToken
                ),
            "File move",
            cancellationToken
        );

        if (
            response.Errors.ValueKind == JsonValueKind.Object
            && response.Errors.TryGetProperty(fileId, out var error)
        )
        {
            throw new HttpRequestException(
                $"Moving file {fileId} into folder {folderId} failed: {error}"
            );
        }

        if (
            response.Files.ValueKind != JsonValueKind.Object
            || !response.Files.TryGetProperty(fileId, out _)
        )
        {
            throw new HttpRequestException($"File {fileId} was not moved into folder {folderId}");
        }
    }

    public async Task VerifyAccessAsync(
        string apiKey,
        string appType,
        CancellationToken cancellationToken
    )
    {
        await SendAsync(
            () =>
                api.SearchFoldersAsync(
                    apiKey,
                    new FolderSearchRequest(appType, Name: null),
                    cancellationToken
                ),
            "Access verification",
            cancellationToken
        );
    }

    private async Task<long?> FindRootFolderIdAsync(
        string apiKey,
        string appType,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var response = await SendAsync(
            () =>
                api.SearchFoldersAsync(
                    apiKey,
                    new FolderSearchRequest(appType, folderName),
                    cancellationToken
                ),
            "Folder search",
            cancellationToken
        );

        return response
            .Data?.FirstOrDefault(folder =>
                folder.ParentFolderId is null
                && string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            )
            ?.Id;
    }

    private async Task<string> SendToStorageAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        using var multipartForm = new MultipartFormDataContent();

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        fileContent.Headers.ContentLength = fileSize;
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "\"file\"",
            FileName = $"\"{fileName}\"",
        };
        multipartForm.Add(fileContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
        {
            Content = multipartForm,
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Storage upload of {fileName} failed with status code {response.StatusCode}: {ExtractErrorMessage(content)}"
            );
        }

        var uploaded = JsonSerializer.Deserialize<UploadFileResponse>(
            content,
            JsonSerializerOptions
        );

        if (uploaded?.Status != SuccessStatus)
        {
            throw new HttpRequestException(
                $"Storage upload of {fileName} failed: {uploaded?.Message ?? content}"
            );
        }

        if (string.IsNullOrWhiteSpace(uploaded.Id))
        {
            throw new HttpRequestException(
                $"Storage accepted {fileName} without returning a file id: {uploaded.Message}"
            );
        }

        return uploaded.Id;
    }

    private async Task<T> SendAsync<T>(
        Func<Task<ApiResponse<T>>> request,
        string operationName,
        CancellationToken cancellationToken
    )
        where T : ICostActionResponse
    {
        return ReadContent(await SendRawAsync(request, cancellationToken), operationName);
    }

    private async Task<ApiResponse<T>> SendRawAsync<T>(
        Func<Task<ApiResponse<T>>> request,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await request();

            if (
                response.StatusCode != HttpStatusCode.TooManyRequests
                || attempt >= RateLimitAttempts
            )
            {
                return response;
            }

            var delay = GetRateLimitDelay(response);

            logger.LogInformation(
                "Rate limited by CostAction API, waiting {Delay} before retrying (Attempt {Attempt})",
                delay,
                attempt
            );

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private TimeSpan GetRateLimitDelay(IApiResponse response)
    {
        var retryAfterSeconds = DeserializeError(response)?.RetryAfterSeconds;
        var delay = retryAfterSeconds is > 0
            ? TimeSpan.FromSeconds(retryAfterSeconds.Value + 1)
            : DefaultRateLimitDelay;

        return delay < MaximumRateLimitDelay ? delay : MaximumRateLimitDelay;
    }

    private static T ReadContent<T>(ApiResponse<T> response, string operationName)
        where T : ICostActionResponse
    {
        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"{operationName} failed with status code {response.StatusCode}: {DeserializeError(response)?.Message ?? GetErrorContent(response)}"
            );
        }

        if (response.Content.Status != SuccessStatus)
        {
            throw new HttpRequestException(
                $"{operationName} failed: {response.Content.Message ?? "unknown error"}"
            );
        }

        return response.Content;
    }

    private static CostActionErrorResponse? DeserializeError(IApiResponse response)
    {
        var content = GetErrorContent(response);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CostActionErrorResponse>(
                content,
                JsonSerializerOptions
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetErrorContent(IApiResponse response)
    {
        return (response.Error as ApiException)?.Content;
    }

    private static string ExtractErrorMessage(string content)
    {
        try
        {
            return JsonSerializer
                    .Deserialize<CostActionErrorResponse>(content, JsonSerializerOptions)
                    ?.Message
                ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}
