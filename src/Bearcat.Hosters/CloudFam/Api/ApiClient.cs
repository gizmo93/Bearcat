using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.CloudFam.Api;

public class ApiClient(
    ICloudFamApi api,
    HttpClientProvider httpClientProvider,
    ILogger<ApiClient> logger
) : ICloudFamApiClient
{
    public const string ApiBaseUrl = "https://cloudfam.io";

    private const string MimeType = "application/octet-stream";

    private const int MaxFileCodesPerRequest = 50;

    private const int FileLookupPageSize = 100;

    private const int MaxFileLookupPages = 20;

    private const int FinalizationRecoveryAttempts = 10;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    public TimeSpan FinalizationRecoveryDelay { get; set; } = TimeSpan.FromSeconds(30);

    public async Task<CloudFamUploadResult> UploadFileAsync(
        CloudFamConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var session = ReadContent(
            await api.CreateUploadSessionAsync(config.ApiKey, cancellationToken),
            "CloudFam upload session creation"
        );

        if (string.IsNullOrWhiteSpace(session.UploadUrl) || string.IsNullOrWhiteSpace(session.Key))
        {
            throw new HttpRequestException(
                "CloudFam upload session response contained no upload url"
            );
        }

        await SendToStorageAsync(session.UploadUrl, stream, fileSize, cancellationToken);

        var finalized = await FinalizeUploadAsync(
            apiKey: config.ApiKey,
            uploadKey: session.Key,
            fileName: fileName,
            fileSize: fileSize,
            cancellationToken: cancellationToken
        );

        if (string.IsNullOrWhiteSpace(finalized.DownloadLink))
        {
            throw new HttpRequestException(
                "CloudFam upload finalization returned no download link"
            );
        }

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            await MoveFileAsync(config.ApiKey, finalized.FileId, folderId, cancellationToken);
        }

        return new CloudFamUploadResult(
            FileId: finalized.FileId.ToString(),
            FileUrl: finalized.DownloadLink
        );
    }

    public async Task<string> CreateFolderAsync(
        CloudFamConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var existingFolderId = await GetExistingFolderIdAsync(
            config.ApiKey,
            folderName,
            cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId.Value.ToString();
        }

        logger.LogInformation("Creating CloudFam folder {FolderName}", folderName);

        var created = ReadContent(
            await api.CreateFolderAsync(
                config.ApiKey,
                new CreateFolderRequest(folderName, ParentId: null),
                cancellationToken
            ),
            "CloudFam folder creation"
        );

        return created.FolderId.ToString();
    }

    public async Task MoveFileToFolderAsync(
        CloudFamConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var fileId = await ResolveFileIdAsync(
            config.ApiKey,
            fileUrl,
            externalId,
            cancellationToken
        );

        await MoveFileAsync(config.ApiKey, fileId, folderId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        CloudFamConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        if (!await IsApiKeyValidAsync(config, cancellationToken))
        {
            throw new HttpRequestException("Invalid credentials");
        }

        var urlsPerFileCode = files
            .DistinctBy(file => file.Url)
            .Select(file => new { FileCode = ExtractFileCode(file.Url), file.Url })
            .Where(entry => entry.FileCode is not null)
            .GroupBy(entry => entry.FileCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Url).ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        var statusPerFileUrl = new Dictionary<string, LinkCheckStatus>();

        foreach (var chunk in urlsPerFileCode.Keys.Chunk(MaxFileCodesPerRequest))
        {
            var response = await api.GetFileInfoAsync(
                config.ApiKey,
                string.Join(',', chunk),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                throw new HttpRequestException(
                    $"CloudFam file info request failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
                );
            }

            foreach (var item in response.Content.Result ?? [])
            {
                if (
                    item.FileCode is null
                    || !urlsPerFileCode.TryGetValue(item.FileCode, out var fileUrls)
                )
                {
                    continue;
                }

                var status = new LinkCheckStatus(
                    IsOnline: item.IsAlive,
                    DownloadCount: item.IsAlive ? item.Downloads : null
                );

                foreach (var fileUrl in fileUrls)
                {
                    statusPerFileUrl[fileUrl] = status;
                }
            }
        }

        return statusPerFileUrl;
    }

    public async Task<bool> IsApiKeyValidAsync(
        CloudFamConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetProfileAsync(config.ApiKey, cancellationToken);

        return response is { StatusCode: HttpStatusCode.OK, Content.Success: true };
    }

    public static string? ExtractFileCode(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var fileCode = uri
            .AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .LastOrDefault()
            ?.Replace(".html", string.Empty, StringComparison.OrdinalIgnoreCase);

        return fileCode is { Length: > 0 } && fileCode.All(char.IsAsciiLetterOrDigit)
            ? fileCode
            : null;
    }

    private async Task<FinalizeUploadResponse> FinalizeUploadAsync(
        string apiKey,
        string uploadKey,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        var response = await api.FinalizeUploadAsync(
            apiKey,
            new FinalizeUploadRequest(uploadKey, fileName, fileSize),
            cancellationToken
        );

        if (response.IsSuccessStatusCode || !IsTimeoutStatusCode(response.StatusCode))
        {
            return ReadContent(response, "CloudFam upload finalization");
        }

        logger.LogWarning(
            "CloudFam upload finalization for {FileName} timed out with status code {StatusCode}, looking for the file CloudFam kept processing",
            fileName,
            (int?)response.StatusCode
        );

        return await FindFinalizedFileAsync(apiKey, fileName, fileSize, cancellationToken)
            ?? throw new HttpRequestException(
                $"CloudFam upload finalization failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
    }

    private async Task<FinalizeUploadResponse?> FindFinalizedFileAsync(
        string apiKey,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        foreach (var attempt in Enumerable.Range(1, FinalizationRecoveryAttempts))
        {
            if (attempt > 1)
            {
                await Task.Delay(FinalizationRecoveryDelay, cancellationToken);
            }

            var response = await api.ListFilesAsync(
                apiKey,
                page: 1,
                limit: FileLookupPageSize,
                query: fileName,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                continue;
            }

            var match = response
                .Content.Data?.Where(file =>
                    string.Equals(file.OriginalFilename, fileName, StringComparison.Ordinal)
                    && file.FileSizeBytes == fileSize
                    && file.DownloadUrl is { Length: > 0 }
                )
                .MaxBy(file => file.Id);

            if (match is null)
            {
                continue;
            }

            logger.LogInformation(
                "CloudFam finished the timed out finalization of {FileName} as file {FileId} uploaded at {UploadedAt}",
                fileName,
                match.Id,
                match.UploadedAt
            );

            return new FinalizeUploadResponse(
                FileId: match.Id,
                Filename: match.OriginalFilename,
                FileSize: match.FileSizeBytes,
                DownloadLink: match.DownloadUrl
            );
        }

        return null;
    }

    private static bool IsTimeoutStatusCode(HttpStatusCode? statusCode)
    {
        return (int?)statusCode is 408 or >= 500;
    }

    private async Task SendToStorageAsync(
        string uploadUrl,
        Stream stream,
        long fileSize,
        CancellationToken cancellationToken
    )
    {
        using var httpClient = httpClientProvider.GetUploadClient();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(MimeType);
        content.Headers.ContentLength = fileSize;

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"CloudFam storage upload failed with status code {response.StatusCode}: {responseContent}"
            );
        }
    }

    private async Task MoveFileAsync(
        string apiKey,
        long fileId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        if (!long.TryParse(folderId, out var targetFolderId))
        {
            throw new HttpRequestException($"Invalid CloudFam folder id: {folderId}");
        }

        var moved = ReadContent(
            await api.MoveFilesAsync(
                apiKey,
                new MoveFilesRequest([fileId], targetFolderId),
                cancellationToken
            ),
            "CloudFam file move"
        );

        if (moved.MovedCount < 1)
        {
            throw new HttpRequestException(
                $"CloudFam did not move file {fileId} into folder {folderId}"
            );
        }
    }

    private async Task<long> ResolveFileIdAsync(
        string apiKey,
        string fileUrl,
        string? externalId,
        CancellationToken cancellationToken
    )
    {
        if (long.TryParse(externalId, out var fileId))
        {
            return fileId;
        }

        var fileCode =
            ExtractFileCode(fileUrl)
            ?? throw new HttpRequestException(
                $"Could not extract CloudFam file code from URL {fileUrl}"
            );

        for (var page = 1; page <= MaxFileLookupPages; page++)
        {
            var response = await api.ListFilesAsync(
                apiKey,
                page,
                FileLookupPageSize,
                query: null,
                cancellationToken
            );

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                throw new HttpRequestException(
                    $"CloudFam file list request failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
                );
            }

            var match = response.Content.Data?.FirstOrDefault(file =>
                string.Equals(file.ShortUrlId, fileCode, StringComparison.OrdinalIgnoreCase)
            );

            if (match is not null)
            {
                return match.Id;
            }

            if (page >= (response.Content.Pagination?.TotalPages ?? page))
            {
                break;
            }
        }

        throw new HttpRequestException($"Could not find a CloudFam file with the code {fileCode}");
    }

    private async Task<long?> GetExistingFolderIdAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var folderList = ReadContent(
            await api.ListFoldersAsync(apiKey, cancellationToken),
            "CloudFam folder list"
        );

        return folderList
            .Folders?.FirstOrDefault(folder =>
                folder.ParentId is null
                && string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            )
            ?.Id;
    }

    private static T ReadContent<T>(ApiResponse<CloudFamResponse<T>> response, string operationName)
    {
        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"{operationName} failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        if (!response.Content.Success || response.Content.Data is null)
        {
            throw new HttpRequestException(
                $"{operationName} failed: {response.Content.Message ?? "no data was returned"}"
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
            var error = JsonSerializer.Deserialize<ErrorResponse>(content, JsonSerializerOptions);

            return error?.Error?.Message ?? error?.Message ?? content;
        }
        catch (JsonException)
        {
            return content;
        }
    }
}
