using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Refit;

namespace Bearcat.Hosters.Fast2Share.Api;

public class ApiClient(
    IFast2ShareApi api,
    HttpClientProvider httpClientProvider,
    HosterFileDownloader fileDownloader,
    ILogger<ApiClient> logger
) : IFast2ShareApiClient
{
    public const string ApiBaseUrl = "https://api.fast2share.com";

    private const string PublicBaseUrl = "https://f2s.im";

    private const string DefaultMetadataKey = "token";

    private const string MimeType = "application/octet-stream";

    private const string StatusCompleted = "completed";

    private const string StatusFailed = "failed";

    private const string StatusNeedHash = "need_hash";

    private const string StatusExists = "exists";

    private const string StatusWait = "wait";

    private const string StatusReady = "ready";

    private const int MaxParallelLinkChecks = 5;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private readonly TusUploader tusUploader = new(httpClientProvider, logger);

    public TimeSpan StatusPollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan StatusPollTimeout { get; set; } = TimeSpan.FromMinutes(15);

    public async Task<Fast2ShareUploadResult> UploadFileAsync(
        Fast2ShareConfig config,
        string fullFileName,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);

        var fingerprint = await FileFingerprint.ComputeFingerprintAsync(
            fullFileName,
            cancellationToken
        );

        var ticket = await CreateUploadAsync(
            authorization: authorization,
            fileName: fileName,
            fileSize: fileSize,
            fingerprint: fingerprint,
            cancellationToken: cancellationToken
        );

        if (IsStatus(ticket.Status, StatusNeedHash))
        {
            logger.LogInformation(
                "Fast2Share reported a possible duplicate for {FileName}, computing the full hash",
                fileName
            );

            var sha256 = await FileFingerprint.ComputeSha256Async(fullFileName, cancellationToken);

            ticket = await ConfirmUploadAsync(
                authorization: authorization,
                uuid: RequireUuid(ticket),
                sha256: sha256,
                cancellationToken: cancellationToken
            );
        }

        var uuid = RequireUuid(ticket);
        var deduped =
            IsStatus(ticket.Status, StatusCompleted) || IsStatus(ticket.Status, StatusExists);
        string fileUrl;

        if (deduped)
        {
            logger.LogInformation(
                "Fast2Share already stores {FileName}, no bytes were transferred",
                fileName
            );

            fileUrl = ticket.ShareUrl ?? BuildFileUrl(uuid);
        }
        else
        {
            await tusUploader.UploadAsync(
                uploadUrl: RequireUploadUrl(ticket),
                uploadToken: RequireUploadToken(ticket),
                metadataKey: ticket.MetadataKey ?? DefaultMetadataKey,
                stream: stream,
                fileSize: fileSize,
                cancellationToken: cancellationToken
            );

            var status = await WaitForCompletionAsync(authorization, uuid, cancellationToken);
            fileUrl = status.ShareUrl ?? BuildFileUrl(uuid);
        }

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            await MoveFileAsync(authorization, uuid, folderId, cancellationToken);
        }

        return new Fast2ShareUploadResult(Uuid: uuid, FileUrl: fileUrl, Deduped: deduped);
    }

    public async Task<string> CreateFolderAsync(
        Fast2ShareConfig config,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);

        var existingFolderId = await GetExistingFolderIdAsync(
            authorization,
            folderName,
            cancellationToken
        );

        if (existingFolderId is not null)
        {
            return existingFolderId.Value.ToString();
        }

        logger.LogInformation("Creating Fast2Share folder {FolderName}", folderName);

        var response = await api.CreateFolderAsync(
            authorization,
            new CreateFolderRequest(folderName, ParentId: null),
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflictingFolderId = await GetExistingFolderIdAsync(
                authorization,
                folderName,
                cancellationToken
            );

            if (conflictingFolderId is not null)
            {
                return conflictingFolderId.Value.ToString();
            }
        }

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"Fast2Share folder creation failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        return response.Content.Id.ToString();
    }

    public async Task MoveFileToFolderAsync(
        Fast2ShareConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        var uuid = externalId is { Length: > 0 } ? externalId : ExtractUuid(fileUrl);

        if (string.IsNullOrWhiteSpace(uuid))
        {
            throw new HttpRequestException(
                $"Could not extract Fast2Share file uuid from URL {fileUrl}"
            );
        }

        await MoveFileAsync(
            GetAuthorizationHeader(config.ApiKey),
            uuid,
            folderId,
            cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        Fast2ShareConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    )
    {
        if (!await IsApiKeyValidAsync(config, cancellationToken))
        {
            throw new HttpRequestException("Invalid credentials");
        }

        var authorization = GetAuthorizationHeader(config.ApiKey);
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var checkTasks = files
            .DistinctBy(file => file.Url)
            .Select(file => CheckLinkAsync(authorization, file, semaphore, cancellationToken))
            .ToList();

        var results = await Task.WhenAll(checkTasks);

        return results
            .Where(result => result.Status is not null)
            .ToDictionary(result => result.FileUrl, result => result.Status!);
    }

    public async Task<bool> IsApiKeyValidAsync(
        Fast2ShareConfig config,
        CancellationToken cancellationToken
    )
    {
        var response = await api.GetUserAsync(
            GetAuthorizationHeader(config.ApiKey),
            cancellationToken
        );

        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task DownloadFileAsync(
        Fast2ShareConfig config,
        string fileUrl,
        string? externalId,
        string targetFilePath,
        IDownloadProgress progress,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    )
    {
        var uuid = externalId is { Length: > 0 } ? externalId : ExtractUuid(fileUrl);

        if (string.IsNullOrWhiteSpace(uuid))
        {
            throw new HttpRequestException(
                $"Could not extract Fast2Share file uuid from URL {fileUrl}"
            );
        }

        var response = await api.GetFileDownloadAsync(
            GetAuthorizationHeader(config.ApiKey),
            uuid,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"Fast2Share download request failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        var downloadTicket = response.Content;

        if (IsStatus(downloadTicket.Status, StatusWait))
        {
            throw new HttpRequestException(
                $"Fast2Share did not issue a download link for file {fileUrl} and asked to wait {downloadTicket.Seconds ?? 0} seconds"
            );
        }

        if (
            !IsStatus(downloadTicket.Status, StatusReady)
            || string.IsNullOrWhiteSpace(downloadTicket.DownloadUrl)
        )
        {
            throw new HttpRequestException(
                $"Fast2Share did not issue a download link for file {fileUrl} and reported status {downloadTicket.Status ?? "unknown"}"
            );
        }

        await fileDownloader.DownloadToFileAsync(
            downloadUrl: downloadTicket.DownloadUrl,
            targetFilePath: targetFilePath,
            progress: progress,
            expectedSizeBytes: expectedSizeBytes ?? downloadTicket.Size,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        Fast2ShareConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        var authorization = GetAuthorizationHeader(config.ApiKey);
        using var semaphore = new SemaphoreSlim(MaxParallelLinkChecks);

        var sizeTasks = fileUrls
            .Distinct()
            .Select(fileUrl =>
                GetFileSizeAsync(authorization, fileUrl, semaphore, cancellationToken)
            )
            .ToList();

        var results = await Task.WhenAll(sizeTasks);

        return results
            .Where(result => result.Size is > 0)
            .ToDictionary(result => result.FileUrl, result => result.Size!.Value);
    }

    private async Task<(string FileUrl, long? Size)> GetFileSizeAsync(
        string authorization,
        string fileUrl,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var uuid = ExtractUuid(fileUrl);

        if (uuid is null)
        {
            return (fileUrl, null);
        }

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var response = await api.GetFileAsync(authorization, uuid, cancellationToken);

            return (fileUrl, response.IsSuccessStatusCode ? response.Content?.Size : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Failed to look up the Fast2Share file size for {FileUrl}: {Message}",
                fileUrl,
                ex.InnerException?.Message ?? ex.Message
            );

            return (fileUrl, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<CreateUploadResponse> CreateUploadAsync(
        string authorization,
        string fileName,
        long fileSize,
        string fingerprint,
        CancellationToken cancellationToken
    )
    {
        var response = await api.CreateUploadAsync(
            authorization,
            new CreateUploadRequest(
                Filename: fileName,
                Size: fileSize,
                Type: MimeType,
                Fingerprint: fingerprint,
                Sha256: null
            ),
            cancellationToken
        );

        return ReadUploadTicket(response, "Fast2Share upload creation");
    }

    private async Task<CreateUploadResponse> ConfirmUploadAsync(
        string authorization,
        string uuid,
        string sha256,
        CancellationToken cancellationToken
    )
    {
        var response = await api.ConfirmUploadAsync(
            authorization,
            uuid,
            new ConfirmUploadRequest(sha256),
            cancellationToken
        );

        return ReadUploadTicket(response, "Fast2Share dedup confirmation");
    }

    private static CreateUploadResponse ReadUploadTicket(
        ApiResponse<CreateUploadResponse> response,
        string operationName
    )
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = DeserializeErrorContent<CreateUploadResponse>(response);

            if (conflict?.Uuid is { Length: > 0 })
            {
                return conflict with { Status = StatusExists };
            }
        }

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"{operationName} failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        return response.Content;
    }

    private async Task<FileStatusResponse> WaitForCompletionAsync(
        string authorization,
        string uuid,
        CancellationToken cancellationToken
    )
    {
        var deadline = DateTimeOffset.UtcNow + StatusPollTimeout;

        while (true)
        {
            var response = await api.GetFileStatusAsync(authorization, uuid, cancellationToken);

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                throw new HttpRequestException(
                    $"Fast2Share upload status request failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
                );
            }

            if (IsStatus(response.Content.Status, StatusCompleted))
            {
                return response.Content;
            }

            if (IsStatus(response.Content.Status, StatusFailed))
            {
                throw new HttpRequestException(
                    $"Fast2Share reported a failed upload for file {uuid}"
                );
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Fast2Share upload {uuid} was still {response.Content.Status} after {StatusPollTimeout}"
                );
            }

            await Task.Delay(StatusPollInterval, cancellationToken);
        }
    }

    private async Task MoveFileAsync(
        string authorization,
        string uuid,
        string folderId,
        CancellationToken cancellationToken
    )
    {
        if (!long.TryParse(folderId, out var targetFolderId))
        {
            throw new HttpRequestException($"Invalid Fast2Share folder id: {folderId}");
        }

        var response = await api.MoveFileAsync(
            authorization,
            uuid,
            new MoveFileRequest(targetFolderId),
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Fast2Share file move failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }
    }

    private async Task<long?> GetExistingFolderIdAsync(
        string authorization,
        string folderName,
        CancellationToken cancellationToken
    )
    {
        var response = await api.ListFoldersAsync(authorization, cancellationToken);

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            throw new HttpRequestException(
                $"Fast2Share folder list failed with status code {response.StatusCode}: {GetErrorMessage(response)}"
            );
        }

        return response
            .Content.Data?.FirstOrDefault(folder =>
                folder.ParentId is null
                && string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)
            )
            ?.Id;
    }

    private async Task<(string FileUrl, LinkCheckStatus? Status)> CheckLinkAsync(
        string authorization,
        FileUrlToCheckDto file,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        var uuid = file.ExternalId is { Length: > 0 } ? file.ExternalId : ExtractUuid(file.Url);

        if (uuid is null)
        {
            return (file.Url, null);
        }

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var response = await api.GetFileAsync(authorization, uuid, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (file.Url, new LinkCheckStatus(IsOnline: false, DownloadCount: null));
            }

            if (!response.IsSuccessStatusCode || response.Content is null)
            {
                return (file.Url, null);
            }

            var isOnline = !IsStatus(response.Content.Status, StatusFailed);

            return (
                file.Url,
                new LinkCheckStatus(isOnline, isOnline ? response.Content.Downloads : null)
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to check Fast2Share link {FileUrl}: {Message}",
                file.Url,
                ex.InnerException?.Message ?? ex.Message
            );

            return (file.Url, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static string? ExtractUuid(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri
            .AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();

        var fileSegmentIndex = segments.FindIndex(segment =>
            string.Equals(segment, "f", StringComparison.OrdinalIgnoreCase)
        );

        if (fileSegmentIndex < 0 || segments.Count <= fileSegmentIndex + 1)
        {
            return null;
        }

        var uuid = segments[fileSegmentIndex + 1];

        return IsValidUuid(uuid) ? uuid : null;
    }

    private static bool IsValidUuid(string value)
    {
        return value.Length is >= 6 and <= 64
            && value.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
            );
    }

    private static string BuildFileUrl(string uuid)
    {
        return $"{PublicBaseUrl}/f/{uuid}";
    }

    private static string RequireUuid(CreateUploadResponse ticket)
    {
        return ticket.Uuid is { Length: > 0 }
            ? ticket.Uuid
            : throw new HttpRequestException("Fast2Share upload response contained no uuid");
    }

    private static string RequireUploadUrl(CreateUploadResponse ticket)
    {
        return ticket.UploadUrl is { Length: > 0 }
            ? ticket.UploadUrl
            : throw new HttpRequestException("Fast2Share upload response contained no upload url");
    }

    private static string RequireUploadToken(CreateUploadResponse ticket)
    {
        return ticket.UploadToken is { Length: > 0 }
            ? ticket.UploadToken
            : throw new HttpRequestException(
                "Fast2Share upload response contained no upload token"
            );
    }

    private static bool IsStatus(string? status, string expected)
    {
        return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAuthorizationHeader(string apiKey)
    {
        return $"Bearer {apiKey}";
    }

    private static string? GetErrorMessage(IApiResponse response)
    {
        return DeserializeErrorContent<ErrorResponse>(response)?.Error ?? GetErrorContent(response);
    }

    private static T? DeserializeErrorContent<T>(IApiResponse response)
        where T : class
    {
        var content = GetErrorContent(response);

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

    private static string? GetErrorContent(IApiResponse response)
    {
        return (response.Error as ApiException)?.Content;
    }
}
